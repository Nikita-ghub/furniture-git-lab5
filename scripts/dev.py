#!/usr/bin/env python3
"""Локальный запуск ЛР №3. Пароли и данные остаются в игнорируемой .local/."""
import argparse
import json
import os
from pathlib import Path
import secrets
import signal
import subprocess
import sys
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / '.local'
PG_BIN = Path(os.environ.get('TP_PG_BIN', '/Library/PostgreSQL/18/bin'))
SERVICES = [('Employees', 5101), ('Orders', 5102), ('Summary', 5103)]


def config():
    LOCAL.mkdir(mode=0o700, exist_ok=True)
    path = LOCAL / 'dev.json'
    if not path.exists():
        values = {key: secrets.token_urlsafe(24) for key in ['pg_password', 'employees_password', 'orders_password', 'admin_password']}
        values['pg_port'] = 55432
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(fd, 'w') as stream:
            json.dump(values, stream, indent=2)
    return json.loads(path.read_text())


def pg_env(cfg):
    return dict(os.environ, PGHOST='127.0.0.1', PGPORT=str(cfg['pg_port']),
                PGUSER='furniture_dev', PGPASSWORD=cfg['pg_password'], PGDATABASE='postgres')


def pg_ready(cfg):
    return subprocess.run([str(PG_BIN / 'pg_isready')], env=pg_env(cfg),
                          stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0


def database_start(cfg):
    data = LOCAL / 'pgdata'
    if not data.exists():
        password_file = LOCAL / 'init-password'
        password_file.write_text(cfg['pg_password'])
        password_file.chmod(0o600)
        try:
            subprocess.run([str(PG_BIN / 'initdb'), '-D', str(data), '--username=furniture_dev',
                            '--auth-local=reject', '--auth-host=scram-sha-256',
                            '--encoding=UTF8', '--locale=C', '--pwfile=' + str(password_file)], check=True)
        finally:
            password_file.unlink(missing_ok=True)
    # Проверяем PID именно нашего кластера; чужой сервер на этом порту не используем.
    status = subprocess.run([str(PG_BIN / 'pg_ctl'), '-D', str(data), 'status'],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if status.returncode != 0:
        if pg_ready(cfg):
            raise RuntimeError('Port 55432 is occupied. Choose another pg_port in .local/dev.json.')
        subprocess.run([str(PG_BIN / 'pg_ctl'), '-D', str(data), '-l', str(LOCAL / 'postgres.log'),
                        '-o', "-h 127.0.0.1 -p %s -c unix_socket_directories=''" % cfg['pg_port'],
                        '-w', 'start'], check=True)
    for role, dbname, password_key in [
        ('furniture_employees', 'furniture_employees', 'employees_password'),
        ('furniture_orders', 'furniture_orders', 'orders_password')]:
        # Значения фиксированы в коде; секреты сгенерированы token_urlsafe и не выводятся.
        sql = "SELECT 1 FROM pg_roles WHERE rolname='%s';" % role
        found = subprocess.check_output([str(PG_BIN / 'psql'), '-XAt', '-c', sql], env=pg_env(cfg), text=True).strip()
        if not found:
            sql = "CREATE ROLE %s LOGIN PASSWORD '%s';" % (role, cfg[password_key])
            subprocess.run([str(PG_BIN / 'psql'), '-X', '-v', 'ON_ERROR_STOP=1'], env=pg_env(cfg), input=sql,
                           text=True, check=True, stdout=subprocess.DEVNULL)
        found = subprocess.check_output([str(PG_BIN / 'psql'), '-XAt', '-c',
            "SELECT 1 FROM pg_database WHERE datname='%s';" % dbname], env=pg_env(cfg), text=True).strip()
        if not found:
            subprocess.run([str(PG_BIN / 'createdb'), '-O', role, dbname], env=pg_env(cfg), check=True)


def service_env(cfg):
    result = dict(os.environ)
    for name, password_key in [('Employees', 'employees_password'), ('Orders', 'orders_password')]:
        db = 'furniture_' + name.lower()
        result['ConnectionStrings__' + name] = (
            'Host=127.0.0.1;Port=%s;Database=%s;Username=%s;Password=%s' %
            (cfg['pg_port'], db, db, cfg[password_key]))
    result['Bootstrap__Password'] = cfg['admin_password']
    result['ASPNETCORE_ENVIRONMENT'] = 'Development'
    return result


def healthy(port):
    try:
        with urllib.request.urlopen('http://127.0.0.1:%s/health' % port, timeout=1) as response:
            return response.status == 200
    except Exception:
        return False


def start(cfg):
    database_start(cfg)
    subprocess.run([str(ROOT / 'scripts/dotnet.sh'), 'build', str(ROOT / 'Furniture.slnx'), '--no-restore', '--nologo',
                    '--disable-build-servers', '-m:1', '-p:UseSharedCompilation=false'],
                   cwd=ROOT, check=True)
    for name, port in SERVICES:
        pidfile = LOCAL / (name.lower() + '.pid')
        if pidfile.exists():
            try:
                os.kill(int(pidfile.read_text()), 0)
                if healthy(port):
                    print(name + ' is already running.')
                    continue
            except ProcessLookupError:
                pass
        if healthy(port):
            raise RuntimeError('Port %s is occupied by another service.' % port)
        dll = ROOT / ('src/Furniture.%s/bin/Debug/net10.0/Furniture.%s.dll' % (name, name))
        with (LOCAL / (name.lower() + '.log')).open('a') as log:
            process = subprocess.Popen([str(ROOT / 'scripts/dotnet.sh'), str(dll),
                                        '--urls', 'http://127.0.0.1:%s' % port],
                cwd=ROOT / ('src/Furniture.' + name), env=service_env(cfg),
                stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        pidfile.write_text(str(process.pid))
        for _ in range(100):
            if healthy(port):
                print('%s: http://127.0.0.1:%s' % (name, port))
                break
            if process.poll() is not None:
                raise RuntimeError('Service exited. See .local/%s.log' % name.lower())
            time.sleep(0.2)
        else:
            raise RuntimeError('Service startup timeout. See .local/%s.log' % name.lower())
    # Готовый локальный environment для импорта в Postman; содержит пароль, не публиковать.
    environment = {'name': 'Furniture Local (private)', 'values': [
        {'key': 'employeesUrl', 'value': 'http://127.0.0.1:5101', 'enabled': True},
        {'key': 'ordersUrl', 'value': 'http://127.0.0.1:5102', 'enabled': True},
        {'key': 'summaryUrl', 'value': 'http://127.0.0.1:5103', 'enabled': True},
        {'key': 'adminLogin', 'value': 'admin', 'enabled': True},
        {'key': 'adminPassword', 'value': cfg['admin_password'], 'enabled': True, 'type': 'secret'}]}
    path = LOCAL / 'Furniture.local.postman_environment.json'
    path.write_text(json.dumps(environment, ensure_ascii=False, indent=2))
    path.chmod(0o600)
    print('Postman environment: .local/Furniture.local.postman_environment.json')


def stop():
    for name, port in reversed(SERVICES):
        path = LOCAL / (name.lower() + '.pid')
        if path.exists():
            try:
                os.kill(int(path.read_text()), signal.SIGTERM)
            except ProcessLookupError:
                pass
            path.unlink()
    data = LOCAL / 'pgdata'
    if data.exists():
        subprocess.run([str(PG_BIN / 'pg_ctl'), '-D', str(data), '-m', 'fast', '-w', 'stop'], check=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('action', choices=['start', 'stop', 'db-start', 'status'])
    args = parser.parse_args()
    if args.action == 'stop':
        stop()
    else:
        cfg = config()
        if args.action == 'start': start(cfg)
        elif args.action == 'db-start': database_start(cfg)
        else:
            print('PostgreSQL:', 'running' if pg_ready(cfg) else 'stopped')
            for name, port in SERVICES:
                print(name + ':', 'running' if healthy(port) else 'stopped')
