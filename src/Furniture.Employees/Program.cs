using Furniture.Employees;
using Furniture.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddFurnitureApi(remoteIdentity: false);
builder.Services.AddDbContext<EmployeesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Employees")));
builder.Services.AddScoped<IPasswordHasher<Employee>, PasswordHasher<Employee>>();
builder.Services.AddScoped<IIdentityVerifier, EmployeeIdentityVerifier>();
var app = builder.Build();
if (!args.Contains("--no-bootstrap"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<EmployeesDbContext>();
    await db.Database.MigrateAsync();
    if (!await db.Employees.AnyAsync())
    {
        var password = builder.Configuration["Bootstrap:Password"];
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            throw new InvalidOperationException("Set Bootstrap__Password (at least 8 characters) for the first launch.");
        var admin = new Employee { FullName = "Администратор", Phone = "+70000000000", Login = "admin", Roles = [EmployeeRole.Administrator] };
        admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Employee>>().HashPassword(admin, password);
        db.Employees.Add(admin);
        await db.SaveChangesAsync();
    }
}
app.UseFurnitureApi("Furniture.Employees");
app.Run();

namespace Furniture.Employees
{
    public sealed class EmployeeIdentityVerifier(EmployeesDbContext db, IPasswordHasher<Employee> hasher) : IIdentityVerifier
    {
        public async Task<EmployeeDto?> VerifyAsync(string authorization, CancellationToken cancellationToken)
        {
            var credentials = BasicCredentials.Parse(authorization);
            if (credentials is null) return null;
            var employee = await db.Employees.SingleOrDefaultAsync(e => e.Login == credentials.Value.Login && e.IsActive, cancellationToken);
            if (employee is null) return null;
            return hasher.VerifyHashedPassword(employee, employee.PasswordHash, credentials.Value.Password) == PasswordVerificationResult.Failed
                ? null : employee.ToDto();
        }
    }
}
