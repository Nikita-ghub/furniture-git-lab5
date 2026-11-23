using Furniture.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.AddFurnitureApi();
builder.Services.AddHttpClient("employees", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Employees"]!);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient("orders", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Orders"]!);
    client.Timeout = TimeSpan.FromSeconds(5);
});
var app = builder.Build();
app.UseFurnitureApi("Furniture.Summary");
app.Run();
