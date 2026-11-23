using System.Security.Claims;
using Furniture.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Furniture.Orders.Controllers;

[ApiController, Authorize(Roles = "Administrator"), Route("api/orders")]
public class OrdersController(OrdersDbContext db, ServiceApi employees) : ControllerBase
{
    private IQueryable<Order> DetailedOrders => db.Orders
        .Include(o => o.Stages).ThenInclude(s => s.Assignments).Include(o => o.History).AsSplitQuery();

    [HttpGet]
    public async Task<ActionResult<OrderDto[]>> GetAll(CancellationToken token) =>
        (await DetailedOrders.AsNoTracking().OrderBy(o => o.Id).ToListAsync(token)).Select(o => o.ToDto()).ToArray();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> Get(int id, CancellationToken token)
    {
        var order = await DetailedOrders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, token);
        return order is null ? NotFound() : Ok(order.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<CreatedOrderDto>> Create(CreateOrderRequest request, CancellationToken token)
    {
        if (request.PlannedCompletionDate is { Kind: DateTimeKind.Unspecified })
            return BadRequest("Укажите дату с часовым поясом, например 2026-11-30T12:00:00Z.");
        if (!await db.Clients.AnyAsync(c => c.Id == request.ClientId, token))
            return BadRequest("Клиент не найден.");

        var staff = await employees.GetAsync<EmployeeDto[]>("api/employees", token)
            ?? throw new DependencyUnavailableException();
        var administratorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var now = DateTime.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(731003)", token);
        var busyIds = await db.Assignments
            .Where(a => a.Status == AssignmentStatus.Assigned || a.Status == AssignmentStatus.Accepted)
            .Select(a => a.EmployeeId).Distinct().ToListAsync(token);
        var lastAssignments = await db.Assignments.GroupBy(a => a.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, LastAssignedAt = g.Max(a => a.AssignedAt) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.LastAssignedAt, token);
        var measurer = MeasurerSelector.Select(
            staff,
            busyIds.ToHashSet(),
            lastAssignments);

        var stage = new OrderStage { Type = StageType.MeasurementAndApproval,
            Status = StageStatus.WaitingForAssignee };
        var order = new Order
        {
            Number = Guid.NewGuid().ToString("N"), ClientId = request.ClientId,
            ResponsibleAdministratorId = administratorId, CreatedAt = now,
            ExecutionAddress = request.ExecutionAddress.Trim(), Description = request.Description.Trim(),
            CustomerWishes = request.CustomerWishes.Trim(),
            PlannedCompletionDate = request.PlannedCompletionDate?.ToUniversalTime(),
            Status = OrderStatus.New, Stages = [stage]
        };
        order.History.Add(new ChangeHistory { EmployeeId = administratorId, ChangedAt = now,
            Action = "OrderCreated", NewValue = OrderStatus.New.ToString() });
        if (measurer is not null)
        {
            stage.Assignments.Add(new EmployeeAssignment { EmployeeId = measurer.Id,
                RoleOnStage = EmployeeRole.Measurer, AssignedAt = now, Status = AssignmentStatus.Assigned });
            stage.Status = StageStatus.Assigned;
            order.Status = OrderStatus.MeasurementAndApproval;
            order.History.Add(new ChangeHistory { ChangedAt = now, Action = "MeasurerAssigned", NewValue = measurer.Id.ToString() });
            order.History.Add(new ChangeHistory { ChangedAt = now, Action = "OrderStatusChanged",
                OldValue = OrderStatus.New.ToString(), NewValue = order.Status.ToString() });
        }
        db.Orders.Add(order);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return CreatedAtAction(nameof(Get), new { id = order.Id },
            new CreatedOrderDto(order.ToDto(), measurer is not null, measurer?.Id));
    }
}
