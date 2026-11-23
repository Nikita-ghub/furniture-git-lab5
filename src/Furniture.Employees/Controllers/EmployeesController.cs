using System.Security.Claims;
using Furniture.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Furniture.Employees.Controllers;

[ApiController, Authorize(Roles = "Administrator"), Route("api/employees")]
public class EmployeesController(EmployeesDbContext db, IPasswordHasher<Employee> hasher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmployeeDto[]>> GetAll(CancellationToken token) =>
        (await db.Employees.AsNoTracking().OrderBy(e => e.Id).ToListAsync(token)).Select(e => e.ToDto()).ToArray();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id, CancellationToken token)
    {
        var employee = await db.Employees.FindAsync([id], token);
        return employee is null ? NotFound() : Ok(employee.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken token)
    {
        if (request.Roles.Any(r => !Enum.IsDefined(r))) return BadRequest("Неизвестная роль.");
        var employee = new Employee
        {
            FullName = request.FullName.Trim(), Phone = request.Phone.Trim(),
            Roles = request.Roles.Distinct().ToArray(), Login = request.Login.ToLowerInvariant()
        };
        employee.PasswordHash = hasher.HashPassword(employee, request.Password);
        db.Employees.Add(employee);
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { return Conflict("Этот логин уже занят."); }
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, employee.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Update(int id, UpdateEmployeeRequest request, CancellationToken token)
    {
        var employee = await db.Employees.FindAsync([id], token);
        if (employee is null) return NotFound();
        employee.FullName = request.FullName.Trim();
        employee.Phone = request.Phone.Trim();
        await db.SaveChangesAsync(token);
        return employee.ToDto();
    }
}

[ApiController, Authorize, Route("api/auth")]
public class AuthController(EmployeesDbContext db) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<EmployeeDto>> Me(CancellationToken token)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return (await db.Employees.SingleAsync(e => e.Id == id, token)).ToDto();
    }
}
