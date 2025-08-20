using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Shared;
using Trading.Common.Enums;

namespace Trading.Application.Tests.Services.Shared;

public class BaseTaskManagerTests
{
    private readonly Mock<ILogger<BaseTaskManager>> _mockLogger;
    private readonly GlobalState _globalState;
    public BaseTaskManagerTests()
    {
        _mockLogger = new Mock<ILogger<BaseTaskManager>>();
        _globalState = new GlobalState(Mock.Of<ILogger<GlobalState>>());
    }

    [Fact]
    public async Task StartAsync_ShouldAddTaskToMonitoring()
    {
        // Arrange
        var taskManager = new BaseTaskManager(_mockLogger.Object, _globalState);
        var taskId = Guid.NewGuid().ToString();
        var executed = false;

        // Act
        using var cts = new CancellationTokenSource();
        await taskManager.StartAsync(
            TaskCategory.Strategy,
            taskId,
            async ct =>
            {
                executed = true;
                await Task.Delay(100, ct);
            },
            cts.Token);

        await Task.Delay(200); // Wait for task execution

        // Assert
        Assert.True(executed);
        Assert.Contains(taskId, taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        await taskManager.StopAsync();
        await taskManager.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTaskAlreadyExists_ShouldNotStartNewTask()
    {
        // Arrange
        var taskManager = new BaseTaskManager(_mockLogger.Object, _globalState);
        var taskId = Guid.NewGuid().ToString();
        var executionCount = 0;

        // Act
        using var cts = new CancellationTokenSource();
        await taskManager.StartAsync(
            TaskCategory.Strategy,
            taskId,
            async ct =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(100, ct);
            },
            cts.Token);

        await taskManager.StartAsync(
            TaskCategory.Strategy,
            taskId,
            async ct =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(100, ct);
            },
            cts.Token);

        await Task.Delay(200);

        // Assert
        Assert.Equal(1, executionCount);
        await taskManager.StopAsync();
        await taskManager.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldRemoveAndCancelTask()
    {
        // Arrange
        var taskManager = new BaseTaskManager(_mockLogger.Object, _globalState);
        var taskId = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        await taskManager.StartAsync(
            TaskCategory.Strategy,
            taskId,
            async ct =>
            {
                await Task.Delay(10 * 1000, ct);
            },
            cts.Token);

        // Act
        await taskManager.StopAsync(TaskCategory.Strategy, taskId);

        _mockLogger.VerifyLoggingOnce(LogLevel.Information, $"Task stopped: Category={TaskCategory.Strategy}, TaskId={taskId}");
        Assert.Empty(taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        await taskManager.StopAsync();
        await taskManager.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_WithCategory_ShouldStopAllTasksInCategory()
    {
        // Arrange
        var taskManager = new BaseTaskManager(_mockLogger.Object, _globalState);
        var taskIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        var executingTasks = 0;

        using var cts = new CancellationTokenSource();
        foreach (var taskId in taskIds)
        {
            await taskManager.StartAsync(
                TaskCategory.Strategy,
                taskId,
                async ct =>
                {
                    Interlocked.Increment(ref executingTasks);
                    try
                    {
                        await Task.Delay(100, ct);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref executingTasks);
                    }
                },
                cts.Token);
        }

        await Task.Delay(2000); // Wait for tasks to start

        // Act
        await taskManager.StopAsync(TaskCategory.Strategy);

        // Assert
        Assert.Equal(0, executingTasks);
        Assert.Empty(taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        await taskManager.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveTaskIds_ShouldReturnCorrectTasksForCategory()
    {
        // Arrange
        var taskManager = new BaseTaskManager(_mockLogger.Object, _globalState);
        var strategyTaskId = Guid.NewGuid().ToString();
        var alertTaskId = Guid.NewGuid().ToString();

        using var cts = new CancellationTokenSource();
        await taskManager.StartAsync(
            TaskCategory.Strategy,
            strategyTaskId,
            ct => Task.Delay(1000, ct),
            cts.Token);

        await taskManager.StartAsync(
            TaskCategory.Alert,
            alertTaskId,
            ct => Task.Delay(1000, ct),
            cts.Token);

        // Act
        var strategyTasks = taskManager.GetActiveTaskIds(TaskCategory.Strategy);
        var alertTasks = taskManager.GetActiveTaskIds(TaskCategory.Alert);

        // Assert
        Assert.Single(strategyTasks);
        Assert.Equal(strategyTaskId, strategyTasks[0]);
        Assert.Single(alertTasks);
        Assert.Equal(alertTaskId, alertTasks[0]);
        await taskManager.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldStopAllTasks()
    {
        // Arrange
        var taskManager = new BaseTaskManager(_mockLogger.Object, _globalState);
        var executingTasks = 0;
        var tasks = new[]
        {
            (TaskCategory.Strategy, Guid.NewGuid().ToString()),
            (TaskCategory.Alert, Guid.NewGuid().ToString())
        };

        using var cts = new CancellationTokenSource();
        foreach (var (category, taskId) in tasks)
        {
            await taskManager.StartAsync(
                category,
                taskId,
                async ct =>
                {
                    Interlocked.Increment(ref executingTasks);
                    try
                    {
                        await Task.Delay(1000, ct);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref executingTasks);
                    }
                },
                cts.Token);
        }

        await Task.Delay(2000); // Wait for tasks to start

        // Act
        await taskManager.StopAsync();

        // Assert
        Assert.Equal(0, executingTasks);
        Assert.Empty(taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        Assert.Empty(taskManager.GetActiveTaskIds(TaskCategory.Alert));
        await taskManager.DisposeAsync();
    }
}
