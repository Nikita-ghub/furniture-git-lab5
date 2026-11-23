using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Furniture.Orders;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(
            Environment.GetEnvironmentVariable("ConnectionStrings__Orders")
            ?? "Host=127.0.0.1;Port=55432;Database=furniture_orders").Options);
}
