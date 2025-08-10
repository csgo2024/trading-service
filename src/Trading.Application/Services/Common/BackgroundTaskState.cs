using System.Collections.Concurrent;
using Trading.Common.Enums;

namespace Trading.Application.Services.Common;

public class TaskInfo
{

#pragma warning disable CS8618
    public string Id { get; set; }
    public Task Task { get; set; }
    public TaskCategory Category { get; set; }
    public CancellationTokenSource Cts { get; set; }
#pragma warning restore CS8618 

}

public interface IBackgroundTaskState
{
    /// <summary>
    /// 尝试添加任务
    /// </summary>
    bool TryAdd(TaskInfo taskInfo);
    /// <summary>
    /// 尝试移除任务
    /// </summary>
    bool TryRemove(string taskId, out TaskInfo? taskInfo);
    /// <summary>
    /// 尝试获取任务
    /// </summary>
    bool TryGetValue(string taskId, out TaskInfo? taskInfo);
    /// <summary>
    /// 获取所有内存中的任务
    /// </summary>
    TaskInfo[] GetAllTasks();
    /// <summary>
    /// 获取所有任务ID（可持久化）
    /// </summary>
    string[] GetAllTaskIds();
}

public class BackgroundTaskState : IBackgroundTaskState
{
    private readonly ConcurrentDictionary<string, byte> _taskIds = new();
    private readonly ConcurrentDictionary<string, TaskInfo> _runtimeTasks = new();

    public bool TryAdd(TaskInfo taskInfo)
    {
        if (_runtimeTasks.TryAdd(taskInfo.Id, taskInfo))
        {
            _taskIds.TryAdd(taskInfo.Id, 0);
            return true;
        }
        return false;
    }

    public bool TryRemove(string taskId, out TaskInfo? taskInfo)
    {
        if (_runtimeTasks.TryRemove(taskId, out taskInfo))
        {
            _taskIds.TryRemove(taskId, out _);
            return true;
        }
        taskInfo = null;
        return false;
    }

    public bool TryGetValue(string taskId, out TaskInfo? taskInfo)
    {
        return _runtimeTasks.TryGetValue(taskId, out taskInfo);
    }

    public TaskInfo[] GetAllTasks()
    {
        return [.. _runtimeTasks.Values];
    }

    public string[] GetAllTaskIds()
    {
        return [.. _taskIds.Keys];
    }
}
