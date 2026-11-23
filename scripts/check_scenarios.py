#!/usr/bin/env python3
"""Дополнительные проверки: параллельное назначение и недоступность сервиса."""
import base64
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import signal
import time
import urllib.error
import urllib.request
import dev

cfg = dev.config()
authorization = 'Basic ' + base64.b64encode(('admin:' + cfg['admin_password']).encode()).decode()


def request(port, path, method='GET', body=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request('http://127.0.0.1:%s%s' % (port, path), data=data, method=method,
        headers={'Authorization': authorization, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=12) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.load(error) if error.headers.get('Content-Type', '').startswith('application/') else None


checks = []
suffix = str(time.time_ns())
code, client = request(5102, '/api/clients', 'POST', {
    'fullName': 'Проверка параллельного назначения', 'phone': '+70000000001', 'defaultAddress': 'Владимир'})
assert code == 201
code, employee = request(5101, '/api/employees', 'POST', {
    'fullName': 'Замерщик для параллельных запросов', 'phone': '+70000000002',
    'roles': ['Measurer'], 'login': 'parallel_' + suffix, 'password': 'LocalDemoPassword2026!'})
assert code == 201
body = {'clientId': client['id'], 'executionAddress': 'Владимир', 'description': 'Параллельный запрос', 'customerWishes': ''}
with ThreadPoolExecutor(max_workers=2) as pool:
    results = list(pool.map(lambda _: request(5102, '/api/orders', 'POST', body), range(2)))
assert all(code == 201 for code, _ in results), results
assigned = [r for _, r in results if r['measurerAssigned']]
assert len(assigned) == 1 and assigned[0]['measurerId'] == employee['id'], results
assert sorted(r['order']['status'] for _, r in results) == ['MeasurementAndApproval', 'New']
checks.append('Два параллельных запроса не назначают одного замерщика дважды: PASS')

before = request(5102, '/api/orders')[1]
code, _ = request(5102, '/api/orders', 'POST', dict(body, plannedCompletionDate='2026-12-01T12:00:00'))
assert code == 400
assert len(request(5102, '/api/orders')[1]) == len(before)
checks.append('Дата без часового пояса отклонена без создания заказа: PASS')

# Останавливаем только процесс Orders, запущенный нашим dev.py, и возвращаем его в finally.
pidfile = dev.LOCAL / 'orders.pid'
os.kill(int(pidfile.read_text()), signal.SIGTERM)
for _ in range(50):
    if not dev.healthy(5102): break
    time.sleep(.1)
try:
    assert request(5103, '/api/summary/orders')[0] == 503
    assert request(5103, '/api/summary/statistics')[0] == 503
    assert request(5101, '/api/employees')[0] == 200
    checks.append('Недоступность Orders даёт 503 в сводке; Employees продолжает работать: PASS')
finally:
    dev.start(cfg)

assert len(request(5102, '/api/orders')[1]) == len(before)
checks.append('Заказы сохраняются в PostgreSQL после перезапуска сервиса: PASS')
output = dev.ROOT / 'verification/scenarios.txt'
output.parent.mkdir(exist_ok=True)
output.write_text('\n'.join(checks) + '\n')
print('\n'.join(checks))
