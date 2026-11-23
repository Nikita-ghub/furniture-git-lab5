using Furniture.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Furniture.Orders.Controllers;

[ApiController, Authorize(Roles = "Administrator"), Route("api/clients")]
public class ClientsController(OrdersDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClientDto[]>> GetAll(CancellationToken token) =>
        (await db.Clients.AsNoTracking().OrderBy(c => c.Id).ToListAsync(token)).Select(c => c.ToDto()).ToArray();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDto>> Get(int id, CancellationToken token)
    {
        var client = await db.Clients.FindAsync([id], token);
        return client is null ? NotFound() : Ok(client.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<ClientDto>> Create(ClientRequest request, CancellationToken token)
    {
        var client = new Client();
        Apply(client, request);
        db.Clients.Add(client);
        await db.SaveChangesAsync(token);
        return CreatedAtAction(nameof(Get), new { id = client.Id }, client.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClientDto>> Update(int id, ClientRequest request, CancellationToken token)
    {
        var client = await db.Clients.FindAsync([id], token);
        if (client is null) return NotFound();
        Apply(client, request);
        await db.SaveChangesAsync(token);
        return client.ToDto();
    }

    private static void Apply(Client client, ClientRequest request)
    {
        client.FullName = request.FullName.Trim();
        client.Phone = request.Phone.Trim();
        client.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        client.DefaultAddress = request.DefaultAddress.Trim();
    }
}
