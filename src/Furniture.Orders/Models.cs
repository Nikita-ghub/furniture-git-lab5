using Furniture.Shared;
using Microsoft.EntityFrameworkCore;

namespace Furniture.Orders;

public class Client
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string DefaultAddress { get; set; } = "";
    public ClientDto ToDto() => new(Id, FullName, Phone, Email, DefaultAddress);
}

public class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int ClientId { get; set; }
    public int ResponsibleAdministratorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ExecutionAddress { get; set; } = "";
    public string Description { get; set; } = "";
    public string CustomerWishes { get; set; } = "";
    public decimal AgreedCost { get; set; }
    public DateTime? PlannedCompletionDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public OrderStatus Status { get; set; }
    public int? ParentOrderId { get; set; }
    public SubOrderReason? SubOrderReason { get; set; }
    public List<OrderStage> Stages { get; set; } = [];
    public List<ChangeHistory> History { get; set; } = [];

    public OrderDto ToDto() => new(Id, Number, ClientId, ResponsibleAdministratorId, CreatedAt,
        ExecutionAddress, Description, CustomerWishes, AgreedCost, PlannedCompletionDate, CompletedAt,
        Status, ParentOrderId, SubOrderReason,
        Stages.OrderBy(s => s.Id).Select(s => new StageDto(s.Id, s.Type, s.Status, s.PlannedDate)).ToArray(),
        Stages.SelectMany(s => s.Assignments).OrderBy(a => a.Id)
            .Select(a => new AssignmentDto(a.Id, a.OrderStageId, a.EmployeeId, a.RoleOnStage, a.AssignedAt, a.Status)).ToArray(),
        History.OrderBy(h => h.Id).Select(h => new HistoryDto(h.Id, h.EmployeeId, h.ChangedAt, h.Action, h.OldValue, h.NewValue)).ToArray());
}

public class OrderStage
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public StageType Type { get; set; }
    public StageStatus Status { get; set; }
    public DateTime? PlannedDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public List<EmployeeAssignment> Assignments { get; set; } = [];
}

public class EmployeeAssignment
{
    public int Id { get; set; }
    public int OrderStageId { get; set; }
    public int EmployeeId { get; set; }
    public EmployeeRole RoleOnStage { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public AssignmentStatus Status { get; set; }
}

public class ChangeHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? EmployeeId { get; set; }
    public DateTime ChangedAt { get; set; }
    public string Action { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Comment { get; set; }
}

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStage> Stages => Set<OrderStage>();
    public DbSet<EmployeeAssignment> Assignments => Set<EmployeeAssignment>();
    public DbSet<ChangeHistory> History => Set<ChangeHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var client = modelBuilder.Entity<Client>();
        client.Property(c => c.FullName).HasMaxLength(200);
        client.Property(c => c.Phone).HasMaxLength(50);
        client.Property(c => c.Email).HasMaxLength(254);
        client.Property(c => c.DefaultAddress).HasMaxLength(500);
        var order = modelBuilder.Entity<Order>();
        order.HasIndex(o => o.Number).IsUnique();
        order.Property(o => o.Number).HasMaxLength(40);
        order.Property(o => o.ExecutionAddress).HasMaxLength(500);
        order.Property(o => o.Description).HasMaxLength(2000);
        order.Property(o => o.CustomerWishes).HasMaxLength(4000);
        order.Property(o => o.AgreedCost).HasPrecision(18, 2);
        order.Property(o => o.Status).HasConversion<string>();
        order.Property(o => o.SubOrderReason).HasConversion<string>();
        order.HasOne<Client>().WithMany().HasForeignKey(o => o.ClientId).OnDelete(DeleteBehavior.Restrict);
        order.HasOne<Order>().WithMany().HasForeignKey(o => o.ParentOrderId).OnDelete(DeleteBehavior.Restrict);
        order.HasMany(o => o.Stages).WithOne().HasForeignKey(s => s.OrderId).OnDelete(DeleteBehavior.Restrict);
        order.HasMany(o => o.History).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Restrict);
        var stage = modelBuilder.Entity<OrderStage>();
        stage.Property(s => s.Type).HasConversion<string>();
        stage.Property(s => s.Status).HasConversion<string>();
        stage.HasMany(s => s.Assignments).WithOne().HasForeignKey(a => a.OrderStageId).OnDelete(DeleteBehavior.Restrict);
        var assignment = modelBuilder.Entity<EmployeeAssignment>();
        assignment.Property(a => a.RoleOnStage).HasConversion<string>();
        assignment.Property(a => a.Status).HasConversion<string>();
        assignment.HasIndex(a => a.EmployeeId);
        // Межсервисные EmployeeId проверяются через API, а не внешним ключом в чужую БД.
    }
}
