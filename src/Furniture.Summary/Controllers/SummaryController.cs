using Furniture.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Furniture.Summary.Controllers;

[ApiController, Authorize(Roles = "Administrator"), Route("api/summary")]
public class SummaryController(IHttpClientFactory clients, IHttpContextAccessor accessor) : ControllerBase
{
    [HttpGet("orders")]
    public async Task<ActionResult<OrderSummaryDto[]>> Orders(CancellationToken token)
    {
        var ordersTask = new ServiceApi(clients.CreateClient("orders"), accessor).GetAsync<OrderDto[]>("api/orders", token);
        var employeesTask = new ServiceApi(clients.CreateClient("employees"), accessor).GetAsync<EmployeeDto[]>("api/employees", token);
        await Task.WhenAll(ordersTask, employeesTask);
        var orders = await ordersTask ?? throw new DependencyUnavailableException();
        var employees = (await employeesTask ?? throw new DependencyUnavailableException()).ToDictionary(e => e.Id);
        if (orders.Any(o => !employees.ContainsKey(o.ResponsibleAdministratorId)))
            return Problem(statusCode: 503, title: "Не удалось получить данные ответственного администратора.");
        return orders.Select(o => new OrderSummaryDto(o.Id, o.Number, o.Status, o.ClientId,
            o.ResponsibleAdministratorId, employees[o.ResponsibleAdministratorId].FullName, o.CreatedAt)).ToArray();
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<StatusCountDto[]>> Statistics(CancellationToken token)
    {
        var orders = await new ServiceApi(clients.CreateClient("orders"), accessor).GetAsync<OrderDto[]>("api/orders", token)
            ?? throw new DependencyUnavailableException();
        return Enum.GetValues<OrderStatus>().Select(status => new StatusCountDto(status, orders.Count(o => o.Status == status))).ToArray();
    }
}
