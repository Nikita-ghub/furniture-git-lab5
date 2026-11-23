using Furniture.Orders;
using Furniture.Shared;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddFurnitureApi();
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Orders")));
builder.Services.AddHttpClient<ServiceApi>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Employees"]!);
    client.Timeout = TimeSpan.FromSeconds(5);
});
var app = builder.Build();
if (!args.Contains("--no-bootstrap"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();
}
app.UseFurnitureApi("Furniture.Orders");
app.Run();
