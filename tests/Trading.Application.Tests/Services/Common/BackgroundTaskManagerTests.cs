using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Common;
using Trading.Common.Enums;

namespace Trading.Application.Tests.Services.Common;

public class BackgroundTaskManagerTests : IAsyncDisposable
{
    private readonly Mock<ILogger<BackgroundTaskManager>> _loggerMock;
    private readonly BackgroundTaskManager _taskManager;
    public BackgroundTaskManagerTests()
    {
        _loggerMock = new Mock<ILogger<BackgroundTaskManager>>();
        _taskManager = new BackgroundTaskManager(_loggerMock.Object, new BackgroundTaskState());
    }

    [Fact]
    public async Task StartAsync_ShouldAddTaskToMonitoring()
    {
        // Arrange
        var taskId = "test-task";
        var executed = false;

        // Act
        using var cts = new CancellationTokenSource();
        await _taskManager.StartAsync(
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
        Assert.Contains(taskId, _taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        await _taskManager.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenTaskAlreadyExists_ShouldNotStartNewTask()
    {
        // Arrange
        var taskId = "test-task";
        var executionCount = 0;

        // Act
        using var cts = new CancellationTokenSource();
        await _taskManager.StartAsync(
            TaskCategory.Strategy,
            taskId,
            async ct =>
            {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(100, ct);
            },
            cts.Token);

        await _taskManager.StartAsync(
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
        await _taskManager.StopAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldRemoveAndCancelTask()
    {
        // Arrange
        var taskId = "test-task";
        var cancellationRequested = false;

        using var cts = new CancellationTokenSource();
        await _taskManager.StartAsync(
            TaskCategory.Strategy,
            taskId,
            async ct =>
            {
                try
                {
                    await Task.Delay(60 * 1000, ct);
                }
                catch (OperationCanceledException)
                {
                    cancellationRequested = true;
                    throw;
                }
            },
            cts.Token);

        // Act
        await _taskManager.StopAsync(TaskCategory.Strategy, taskId);

        // Assert
        Assert.True(cancellationRequested);
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        await _taskManager.StopAsync();
    }

    [Fact]
    public async Task StopAsync_WithCategory_ShouldStopAllTasksInCategory()
    {
        // Arrange
        var taskIds = new[] { "task1", "task2" };
        var executingTasks = 0;

        using var cts = new CancellationTokenSource();
        foreach (var taskId in taskIds)
        {
            await _taskManager.StartAsync(
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
        await _taskManager.StopAsync(TaskCategory.Strategy);

        // Assert
        Assert.Equal(0, executingTasks);
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategory.Strategy));
    }

    [Fact]
    public async Task GetActiveTaskIds_ShouldReturnCorrectTasksForCategory()
    {
        // Arrange
        var strategyTaskId = "strategy-task";
        var alertTaskId = "alert-task";

        using var cts = new CancellationTokenSource();
        await _taskManager.StartAsync(
            TaskCategory.Strategy,
            strategyTaskId,
            ct => Task.Delay(1000, ct),
            cts.Token);

        await _taskManager.StartAsync(
            TaskCategory.Alert,
            alertTaskId,
            ct => Task.Delay(1000, ct),
            cts.Token);

        // Act
        var strategyTasks = _taskManager.GetActiveTaskIds(TaskCategory.Strategy);
        var alertTasks = _taskManager.GetActiveTaskIds(TaskCategory.Alert);

        // Assert
        Assert.Single(strategyTasks);
        Assert.Equal(strategyTaskId, strategyTasks[0]);
        Assert.Single(alertTasks);
        Assert.Equal(alertTaskId, alertTasks[0]);
    }

    [Fact]
    public async Task StopAsync_ShouldStopAllTasks()
    {
        // Arrange
        var executingTasks = 0;
        var tasks = new[]
        {
            (TaskCategory.Strategy, "strategy-task"),
            (TaskCategory.Alert, "alert-task")
        };

        using var cts = new CancellationTokenSource();
        foreach (var (category, taskId) in tasks)
        {
            await _taskManager.StartAsync(
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
        await _taskManager.StopAsync();

        // Assert
        Assert.Equal(0, executingTasks);
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategory.Strategy));
        Assert.Empty(_taskManager.GetActiveTaskIds(TaskCategory.Alert));
    }

    public async ValueTask DisposeAsync()
    {
        await _taskManager.DisposeAsync();
    }
}
