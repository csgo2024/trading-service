using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Shared;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Tests.Services.Shared;

public class GlobalStateTests
{
    private readonly Mock<ILogger<GlobalState>> _mockLogger;
    private readonly GlobalState _globalState;

    public GlobalStateTests()
    {
        _mockLogger = new Mock<ILogger<GlobalState>>();
        _globalState = new GlobalState(_mockLogger.Object);
    }

    #region Stream State Tests

    [Fact]
    public void StreamState_WhenAddingNewSymbols_ShouldAddAndReturnTrue()
    {
        // Arrange
        var symbols = new[] { "BTCUSDT", "ETHUSDT" };

        // Act
        var result = _globalState.TryAddSymbols(symbols);
        var allSymbols = _globalState.GetAllSymbols();

        // Assert
        Assert.True(result);
        Assert.Equal(2, allSymbols.Count);
        Assert.Contains("BTCUSDT", allSymbols);
        Assert.Contains("ETHUSDT", allSymbols);
    }

    [Fact]
    public void StreamState_WhenAddingDuplicateSymbols_ShouldNotAddAndReturnFalse()
    {
        // Arrange
        var symbols = new[] { "BTCUSDT" };
        _globalState.TryAddSymbols(symbols);

        // Act
        var result = _globalState.TryAddSymbols(symbols);
        var allSymbols = _globalState.GetAllSymbols();

        // Assert
        Assert.False(result);
        Assert.Single(allSymbols);
    }

    [Fact]
    public void StreamState_WhenCleared_ShouldResetAllStreamState()
    {
        // Arrange
        _globalState.TryAddSymbols(new[] { "BTCUSDT" });
        _globalState.TryAddIntervals(new[] { "1m" });

        // Act
        _globalState.ClearStreamState();

        // Assert
        Assert.Empty(_globalState.GetAllSymbols());
        Assert.Empty(_globalState.GetAllIntervals());
        Assert.Null(_globalState.CurrentSubscription);
    }

    #endregion

    #region Background Task Tests

    [Fact]
    public void TaskState_WhenAddingNewTask_ShouldAddSuccessfully()
    {
        // Arrange
        var taskInfo = new TaskInfo
        {
            Id = "task1",
            Category = TaskCategory.Alert,
            Task = Task.CompletedTask,
            Cts = new CancellationTokenSource()
        };

        // Act
        var result = _globalState.TryAddTask(taskInfo);
        var allTasks = _globalState.GetAllTasks();

        // Assert
        Assert.True(result);
        Assert.Single(allTasks);
        Assert.Equal("task1", allTasks[0].Id);
    }

    [Fact]
    public void TaskState_WhenRemovingTask_ShouldRemoveSuccessfully()
    {
        // Arrange
        var taskInfo = new TaskInfo
        {
            Id = "task1",
            Category = TaskCategory.Alert,
            Task = Task.CompletedTask,
            Cts = new CancellationTokenSource()
        };
        _globalState.TryAddTask(taskInfo);

        // Act
        var result = _globalState.TryRemoveTask("task1", out var removedTask);

        // Assert
        Assert.True(result);
        Assert.NotNull(removedTask);
        Assert.Equal("task1", removedTask.Id);
        Assert.Empty(_globalState.GetAllTasks());
    }

    #endregion

    #region Strategy State Tests

    [Fact]
    public void StrategyState_WhenAddingNewStrategy_ShouldAddSuccessfully()
    {
        // Arrange
        var strategy = new Strategy { Id = "strategy1" };

        // Act
        var result = _globalState.AddOrUpdateStrategy(strategy.Id, strategy);
        var allStrategies = _globalState.GetAllStrategies();

        // Assert
        Assert.True(result);
        Assert.Single(allStrategies);
        Assert.Equal("strategy1", allStrategies[0].Id);
    }

    [Fact]
    public void StrategyState_WhenUpdatingStrategy_ShouldUpdateSuccessfully()
    {
        // Arrange
        var strategy = new Strategy { Id = "strategy1", Symbol = "BTCUSDT" };
        _globalState.AddOrUpdateStrategy(strategy.Id, strategy);

        var updatedStrategy = new Strategy { Id = "strategy1", Symbol = "ETHUSDT" };

        // Act
        var result = _globalState.AddOrUpdateStrategy(strategy.Id, updatedStrategy);
        _globalState.TryGetStrategy(strategy.Id, out var retrievedStrategy);

        // Assert
        Assert.False(result); // Should return false because it's an update, not a new addition
        Assert.NotNull(retrievedStrategy);
        Assert.Equal("ETHUSDT", retrievedStrategy.Symbol);
    }

    [Fact]
    public void StrategyState_WhenRemovingStrategy_ShouldRemoveSuccessfully()
    {
        // Arrange
        var strategy = new Strategy { Id = "strategy1" };
        _globalState.AddOrUpdateStrategy(strategy.Id, strategy);

        // Act
        var result = _globalState.TryRemoveStrategy(strategy.Id, out var removedStrategy);

        // Assert
        Assert.True(result);
        Assert.NotNull(removedStrategy);
        Assert.Equal("strategy1", removedStrategy.Id);
        Assert.Empty(_globalState.GetAllStrategies());
    }

    public static TheoryData<DateTime, DateTime> GetNextReconnectTestCases()
    {
        var data = new TheoryData<DateTime, DateTime>();

        // 01:00 前 -> 当天 01:00
        data.Add(new DateTime(2025, 9, 18, 0, 0, 0, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 1, 0, 0, DateTimeKind.Utc));

        // 正好 01:00 -> 01:00
        data.Add(new DateTime(2025, 9, 18, 1, 0, 0, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 1, 0, 0, DateTimeKind.Utc));

        // 01:00 之后 -> 当天 13:00
        data.Add(new DateTime(2025, 9, 18, 1, 0, 1, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc));

        // 13:00 前 -> 当天 13:00
        data.Add(new DateTime(2025, 9, 18, 12, 59, 59, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc));

        // 正好 13:00 -> 13:00
        data.Add(new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc));

        // 晚于 13:00 -> 次日 01:00
        data.Add(new DateTime(2025, 9, 18, 13, 0, 1, DateTimeKind.Utc),
                 new DateTime(2025, 9, 19, 1, 0, 0, DateTimeKind.Utc));

        // 跨年 -> 次年 01:00
        data.Add(new DateTime(2024, 12, 31, 22, 30, 0, DateTimeKind.Utc),
                 new DateTime(2025, 1, 1, 1, 0, 0, DateTimeKind.Utc));

        // 闰日 -> 次日 01:00
        data.Add(new DateTime(2020, 2, 29, 23, 59, 59, DateTimeKind.Utc),
                 new DateTime(2020, 3, 1, 1, 0, 0, DateTimeKind.Utc));

        return data;
    }

    [Theory]
    [MemberData(nameof(GetNextReconnectTestCases))]
    public void GetNextReconnectTime_ReturnsExpectedUtc(DateTime nowUtc, DateTime expectedUtc)
    {
        var result = _globalState.NextReconnectTime(nowUtc);

        Assert.Equal(expectedUtc, result);                  // 精确等于
        Assert.Equal(DateTimeKind.Utc, result.Kind);        // Kind 为 UTC
    }

    [Fact]
    public void ReturnedTime_IsAlwaysOn9Or21AndZeroMinutesSeconds()
    {
        var now = new DateTime(2024, 12, 31, 22, 30, 0, DateTimeKind.Utc);
        var result = _globalState.NextReconnectTime(now).AddHours(8); // UTC+8

        Assert.True(result.Hour == 9 || result.Hour == 21, $"返回小时应为 9 或 21，实际为 {result.Hour}");
        Assert.Equal(0, result.Minute);
        Assert.Equal(0, result.Second);
        Assert.Equal(0, result.Millisecond);
    }

    #endregion
}
