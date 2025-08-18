using Microsoft.Extensions.Logging;
using Trading.Common.Enums;

namespace Trading.Application.Services.Common;

public interface IBackgroundTaskManager : IAsyncDisposable
{
    Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken);
    Task StopAsync(TaskCategory category, string taskId);
    Task StopAsync(TaskCategory category);
    Task StopAsync();
    string[] GetActiveTaskIds(TaskCategory category);
}

public class BackgroundTaskManager : IBackgroundTaskManager
{
    private readonly ILogger<BackgroundTaskManager> _logger;
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private readonly IBackgroundTaskState _state;

    public BackgroundTaskManager(ILogger<BackgroundTaskManager> logger, IBackgroundTaskState state)
    {
        _logger = logger;
        _state = state;
    }

    public async Task StartAsync(TaskCategory category, string taskId, Func<CancellationToken, Task> executionFunc, CancellationToken cancellationToken)
    {
        await _taskLock.WaitAsync(cancellationToken);
        try
        {
            if (_state.TryGetValue(taskId, out _))
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

            if (!_state.TryAdd(taskInfo))
            {
                _logger.LogWarning("Failed to add task info: Category={Category}, TaskId={TaskId}", category, taskId);
                await cts.CancelAsync();
                cts.Dispose();
                return;
            }

            _logger.LogInformation("Task started: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting task: Category={Category}, TaskId={TaskId}", category, taskId);
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
            if (!_state.TryRemove(taskId, out taskInfo))
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
            _logger.LogInformation("Task stopped: Category={Category}, TaskId={TaskId}", category, taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping task: Category={Category}, TaskId={TaskId}", category, taskId);
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
        _logger.LogInformation("All tasks stopped for category: {Category}", category);
    }

    public async Task StopAsync()
    {
        foreach (var item in _state.GetAllTasks())
        {
            await StopAsync(item.Category, item.Id);
        }
        _logger.LogInformation("All tasks stopped across all categories");
    }

    public string[] GetActiveTaskIds(TaskCategory category) => [.. _state.GetAllTasks().Where(k => k.Category == category).Select(k => k.Id)];

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _taskLock.Dispose();
    }
}

