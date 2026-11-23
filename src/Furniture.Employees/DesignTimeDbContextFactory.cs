using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Furniture.Employees;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EmployeesDbContext>
{
    public EmployeesDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<EmployeesDbContext>().UseNpgsql(
            Environment.GetEnvironmentVariable("ConnectionStrings__Employees")
            ?? "Host=127.0.0.1;Port=55432;Database=furniture_employees").Options);
}
