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
        _globalState.TryGetTask(taskId, out var taskInfo);

        // Assert
        Assert.True(executed);
        Assert.NotNull(taskInfo);
        Assert.Equal(taskId, taskInfo.Id);
        _mockLogger.VerifyLoggingOnce(LogLevel.Debug, $"Task started: Category={TaskCategory.Strategy}, TaskId={taskId}");
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
        _mockLogger.VerifyLoggingOnce(LogLevel.Debug, $"Task started: Category={TaskCategory.Strategy}, TaskId={taskId}");
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
        _globalState.TryGetTask(taskId, out var taskInfo);

        // Assert
        Assert.Null(taskInfo);
        _mockLogger.VerifyLoggingOnce(LogLevel.Debug, $"Task stopped: Category={TaskCategory.Strategy}, TaskId={taskId}");
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
        foreach (var taskId in taskIds)
        {
            _globalState.TryGetTask(taskId, out var taskInfo);
            _mockLogger.VerifyLoggingOnce(LogLevel.Debug, $"Task stopped: Category={TaskCategory.Strategy}, TaskId={taskId}");
            Assert.Null(taskInfo);
        }
        Assert.Equal(0, executingTasks);
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
        foreach (var taskId in tasks.Select(t => t.Item2))
        {
            _globalState.TryGetTask(taskId, out var taskInfo);
            Assert.Null(taskInfo);
        }
        Assert.Equal(0, executingTasks);
        await taskManager.DisposeAsync();
    }
}
