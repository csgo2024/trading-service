using Microsoft.Extensions.Logging;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;

namespace Trading.Application.Services.Shared;

public class TaskInfo
{

#pragma warning disable CS8618
    public string Id { get; set; }
    public Task Task { get; set; }
    public TaskCategory Category { get; set; }
    public CancellationTokenSource Cts { get; set; }
#pragma warning restore CS8618

}
public interface ITaskManager : IAsyncDisposable
{
    Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken);
    Task StopAsync(TaskCategory category, string taskId);
    Task StopAsync(TaskCategory category);
    Task StopAsync();
}

public class BaseTaskManager : ITaskManager
{
    private readonly ILogger<BaseTaskManager> _logger;
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private readonly GlobalState _state;

    public BaseTaskManager(ILogger<BaseTaskManager> logger, GlobalState state)
    {
        _logger = logger;
        _state = state;
    }

    public async Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken)
    {
        await _taskLock.WaitAsync(cancellationToken);
        try
        {
            if (_state.TryGetTask(taskId, out _))
            {
                return;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var task = executionFunc(cts.Token);

            var taskInfo = new TaskInfo
            {
                Id = taskId,
                Category = category,
                Cts = cts,
                Task = task
            };

            if (!_state.TryAddTask(taskInfo))
            {
                _logger.LogWarning("Failed to add task info: Category={Category}, TaskId={TaskId}", category, taskId);
                await cts.CancelAsync();
                cts.Dispose();
                return;
            }

            _logger.LogDebug("Task started: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogErrorNotification(ex, "Error starting task: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        finally
        {
            _taskLock.Release();
        }
    }

    public async Task StopAsync(TaskCategory category, string taskId)
    {
        TaskInfo? taskInfo;

        await _taskLock.WaitAsync();
        try
        {
            if (!_state.TryRemoveTask(taskId, out taskInfo))
            {
                return;
            }
        }
        finally
        {
            _taskLock.Release();
        }

        try
        {
            await taskInfo!.Cts.CancelAsync();
            try
            {
                await taskInfo.Task; // Ensure task completes
            }
            catch (OperationCanceledException)
            {
                // ignore TaskCanceledException
            }
            _logger.LogDebug("Task stopped: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogErrorNotification(ex, "Error stopping task: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        finally
        {
            taskInfo!.Cts.Dispose();
        }
    }

    public async Task StopAsync(TaskCategory category)
    {
        foreach (var item in _state.GetAllTasks())
        {
            if (item.Category == category)
            {
                await StopAsync(category, item.Id);
            }
        }
        _logger.LogDebug("All tasks stopped for category: {Category}", category);
    }

    public async Task StopAsync()
    {
        foreach (var item in _state.GetAllTasks())
        {
            await StopAsync(item.Category, item.Id);
        }
        _logger.LogDebug("All tasks stopped across all categories");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _taskLock.Dispose();
    }
}

