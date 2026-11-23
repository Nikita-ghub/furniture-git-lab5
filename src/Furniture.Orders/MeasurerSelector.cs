using Furniture.Shared;

namespace Furniture.Orders;

public static class MeasurerSelector
{
    public static EmployeeDto? Select(
        IEnumerable<EmployeeDto> employees,
        IReadOnlySet<int> busyEmployeeIds,
        IReadOnlyDictionary<int, DateTime>? lastAssignedAt = null)
    {
        return employees
            .Where(employee =>
                employee.IsActive &&
                employee.Roles.Contains(EmployeeRole.Measurer) &&
                !busyEmployeeIds.Contains(employee.Id))
            .OrderBy(employee =>
                lastAssignedAt?.GetValueOrDefault(
                    employee.Id,
                    DateTime.MinValue) ?? DateTime.MinValue)
            .ThenBy(employee => employee.Id)
            .FirstOrDefault();
    }
}