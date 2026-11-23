using Furniture.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Furniture.Employees;

public class Employee
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public EmployeeRole[] Roles { get; set; } = [];
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public EmployeeDto ToDto() => new(Id, FullName, Phone, Roles, Login, IsActive);
}

public class EmployeesDbContext(DbContextOptions<EmployeesDbContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var employee = modelBuilder.Entity<Employee>();
        employee.HasIndex(e => e.Login).IsUnique();
        employee.Property(e => e.FullName).HasMaxLength(200);
        employee.Property(e => e.Phone).HasMaxLength(50);
        employee.Property(e => e.Login).HasMaxLength(64);
        employee.Property(e => e.Roles).HasConversion(
            roles => roles.Select(r => (int)r).ToArray(),
            values => values.Select(v => (EmployeeRole)v).ToArray())
            .Metadata.SetValueComparer(new ValueComparer<EmployeeRole[]>(
                (a, b) => a!.SequenceEqual(b!),
                a => a.Aggregate(0, (hash, role) => HashCode.Combine(hash, role)),
                a => a.ToArray()));
    }
}
