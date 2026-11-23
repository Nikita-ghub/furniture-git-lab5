using Furniture.Orders;
using Furniture.Shared;

namespace Furniture.Orders.Tests;

public class MeasurerSelectorTests
{
    [Test]
    public void Select_ReturnsActiveMeasurer()
    {
        var employee = new EmployeeDto(
            1, "Анна Иванова", "+70000000000",
            [EmployeeRole.Measurer], "anna", true);

        var result = MeasurerSelector.Select(
            [employee],
            new HashSet<int>());

        Assert.That(result, Is.EqualTo(employee));
    }

    [Test]
    public void Select_SkipsBusyMeasurer()
    {
        var busyMeasurer = new EmployeeDto(
            1, "Анна Иванова", "+70000000000",
            [EmployeeRole.Measurer], "anna", true);

        var freeMeasurer = new EmployeeDto(
            2, "Павел Петров", "+70000000001",
            [EmployeeRole.Measurer], "pavel", true);

        var result = MeasurerSelector.Select(
            [busyMeasurer, freeMeasurer],
            new HashSet<int> { busyMeasurer.Id });

        Assert.That(result, Is.EqualTo(freeMeasurer));
    }

    [Test]
    public void Select_PrefersMeasurerAssignedLongestAgo()
    {
        var recentlyAssigned = new EmployeeDto(
            3, "Ирина Соколова", "+70000000002",
            [EmployeeRole.Measurer], "irina", true);

        var longestWaiting = new EmployeeDto(
            4, "Олег Смирнов", "+70000000003",
            [EmployeeRole.Measurer], "oleg", true);

        var lastAssignedAt = new Dictionary<int, DateTime>
        {
            [recentlyAssigned.Id] = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc),
            [longestWaiting.Id] = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var result = MeasurerSelector.Select(
            [recentlyAssigned, longestWaiting],
            new HashSet<int>(),
            lastAssignedAt);

        Assert.That(result, Is.EqualTo(longestWaiting));
    }

    [Test]
    public void Select_PrefersMeasurerNeverAssigned()
    {
        var previouslyAssigned = new EmployeeDto(
            5, "Мария Кузнецова", "+70000000004",
            [EmployeeRole.Measurer], "maria", true);

        var neverAssigned = new EmployeeDto(
            6, "Денис Волков", "+70000000005",
            [EmployeeRole.Measurer], "denis", true);

        var lastAssignedAt = new Dictionary<int, DateTime>
        {
            [previouslyAssigned.Id] =
                new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var result = MeasurerSelector.Select(
            [previouslyAssigned, neverAssigned],
            new HashSet<int>(),
            lastAssignedAt);

        Assert.That(result, Is.EqualTo(neverAssigned));
    }

[Test]
    public void Select_UsesEmployeeIdWhenAssignmentTimesAreEqual()
    {
        var higherId = new EmployeeDto(
            8, "Сергей Иванов", "+70000000006",
            [EmployeeRole.Measurer], "sergey", true);

        var lowerId = new EmployeeDto(
            7, "Татьяна Орлова", "+70000000007",
            [EmployeeRole.Measurer], "tatyana", true);

        var sameAssignedAt = new DateTime(
            2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

        var lastAssignedAt = new Dictionary<int, DateTime>
        {
            [higherId.Id] = sameAssignedAt,
            [lowerId.Id] = sameAssignedAt
        };

        var result = MeasurerSelector.Select(
            [higherId, lowerId],
            new HashSet<int>(),
            lastAssignedAt);

        Assert.That(result, Is.EqualTo(lowerId));
    }
}