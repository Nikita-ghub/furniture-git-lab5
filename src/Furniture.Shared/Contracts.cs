using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Furniture.Shared;

public enum EmployeeRole { Administrator, Measurer, ProductionWorker, Courier, Assembler }
public enum OrderStatus { New, MeasurementAndApproval, InProduction, ReadyForDelivery, Delivery, Assembly, AwaitingPayment, Completed, Cancelled }
public enum StageType { MeasurementAndApproval, Production, Delivery, Assembly }
public enum StageStatus { WaitingPreviousStage, WaitingForAssignee, Assigned, InProgress, Completed, Skipped }
public enum AssignmentStatus { Assigned, Accepted, Completed, Cancelled }
public enum SubOrderReason { Addition, MissingPart, Defect, Rework }

public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}

public record EmployeeDto(int Id, string FullName, string Phone, EmployeeRole[] Roles, string Login, bool IsActive);

public class CreateEmployeeRequest
{
    [Required, MaxLength(200)] public string FullName { get; set; } = "";
    [Required, MaxLength(50)] public string Phone { get; set; } = "";
    [Required, MinLength(1)] public EmployeeRole[] Roles { get; set; } = [];
    [Required, RegularExpression("^[a-zA-Z0-9._-]{3,64}$")] public string Login { get; set; } = "";
    [Required, MinLength(8), MaxLength(200)] public string Password { get; set; } = "";
}

// В этой версии изменяются контактные сведения; роли задаются при регистрации.
public class UpdateEmployeeRequest
{
    [Required, MaxLength(200)] public string FullName { get; set; } = "";
    [Required, MaxLength(50)] public string Phone { get; set; } = "";
}

public class ClientRequest
{
    [Required, MaxLength(200)] public string FullName { get; set; } = "";
    [Required, MaxLength(50)] public string Phone { get; set; } = "";
    [EmailAddress, MaxLength(254)] public string? Email { get; set; }
    [Required, MaxLength(500)] public string DefaultAddress { get; set; } = "";
}

public record ClientDto(int Id, string FullName, string Phone, string? Email, string DefaultAddress);

public class CreateOrderRequest
{
    [Range(1, int.MaxValue)] public int ClientId { get; set; }
    [Required, MaxLength(500)] public string ExecutionAddress { get; set; } = "";
    [Required, MaxLength(2000)] public string Description { get; set; } = "";
    [MaxLength(4000)] public string CustomerWishes { get; set; } = "";
    public DateTime? PlannedCompletionDate { get; set; }
}

public record StageDto(int Id, StageType Type, StageStatus Status, DateTime? PlannedDate);
public record AssignmentDto(int Id, int OrderStageId, int EmployeeId, EmployeeRole RoleOnStage, DateTime AssignedAt, AssignmentStatus Status);
public record HistoryDto(int Id, int? EmployeeId, DateTime ChangedAt, string Action, string? OldValue, string? NewValue);
public record OrderDto(int Id, string Number, int ClientId, int ResponsibleAdministratorId,
    DateTime CreatedAt, string ExecutionAddress, string Description, string CustomerWishes,
    decimal AgreedCost, DateTime? PlannedCompletionDate, DateTime? CompletedAt, OrderStatus Status,
    int? ParentOrderId, SubOrderReason? SubOrderReason, StageDto[] Stages,
    AssignmentDto[] Assignments, HistoryDto[] History);
public record CreatedOrderDto(OrderDto Order, bool MeasurerAssigned, int? MeasurerId);
public record OrderSummaryDto(int Id, string Number, OrderStatus Status, int ClientId,
    int ResponsibleAdministratorId, string ResponsibleAdministratorName, DateTime CreatedAt);
public record StatusCountDto(OrderStatus Status, int Count);
