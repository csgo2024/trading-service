using System.Collections.Concurrent;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Shared;

internal sealed class AlertState
{
    private readonly ConcurrentDictionary<string, Alert> _activeAlerts = new();

    public bool AddOrUpdate(string id, Alert alert)
    {
        _activeAlerts.AddOrUpdate(id, alert, (_, _) => alert);
        return true;
    }

    public bool TryGetValue(string key, out Alert? alert)
    {
        return _activeAlerts.TryGetValue(key, out alert);
    }
    public bool TryRemove(string id)
    {
        var result = _activeAlerts.TryRemove(id, out _);
        return result;
    }

    public void Clear()
    {
        _activeAlerts.Clear();
    }
}

internal sealed class KlineState
{
    private readonly ConcurrentDictionary<string, IBinanceKline> _lastKlines = new();

    public void AddOrUpdate(string key, IBinanceKline kline)
    {
        _lastKlines.AddOrUpdate(key, kline, (_, _) => kline);
    }

    public bool TryGetValue(string key, out IBinanceKline? kline)
    {
        return _lastKlines.TryGetValue(key, out kline);
    }

    public bool TryRemove(string key)
    {
        return _lastKlines.TryRemove(key, out _);
    }
    public void Clear()
    {
        _lastKlines.Clear();
    }
}

internal sealed class StreamState
{
    private readonly HashSet<string> _symbols = new();
    private readonly HashSet<string> _intervals = new();
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromMinutes(12 * 60);

    public DateTime? LastConnectionTime { get; set; }
    public UpdateSubscription? CurrentSubscription { get; set; }

    public bool TryAddSymbols(IEnumerable<string> symbols)
    {
        var before = _symbols.Count;
        _symbols.UnionWith(symbols);
        if (_symbols.Count > before)
        {
            return true;
        }
        return false;
    }

    public bool TryAddIntervals(IEnumerable<string> intervals)
    {
        var before = _intervals.Count;
        _intervals.UnionWith(intervals);
        if (_intervals.Count > before)
        {
            return true;
        }
        return false;
    }

    public HashSet<string> GetAllSymbols() => [.. _symbols];
    public HashSet<string> GetAllIntervals() => [.. _intervals];

    public void ClearStreamState()
    {
        _symbols.Clear();
        _intervals.Clear();
        CurrentSubscription = null;
        LastConnectionTime = null;
    }

    public bool NeedsReconnection()
    {
        if (LastConnectionTime == null)
        {
            return true;
        }
        return (DateTime.UtcNow - LastConnectionTime.Value) > _reconnectInterval;
    }
}

internal sealed class TaskState
{
    private readonly ConcurrentDictionary<string, TaskInfo> _runtimeTasks = new();
    private readonly ConcurrentDictionary<string, byte> _taskIds = new();

    public bool TryAddTask(TaskInfo taskInfo)
    {
        if (_runtimeTasks.TryAdd(taskInfo.Id, taskInfo))
        {
            _taskIds.TryAdd(taskInfo.Id, 0);
            return true;
        }
        return false;
    }

    public bool TryRemoveTask(string taskId, out TaskInfo? taskInfo)
    {
        if (_runtimeTasks.TryRemove(taskId, out taskInfo))
        {
            _taskIds.TryRemove(taskId, out _);
            return true;
        }
        return false;
    }

    public bool TryGetTask(string taskId, out TaskInfo? taskInfo) =>
        _runtimeTasks.TryGetValue(taskId, out taskInfo);

    public TaskInfo[] GetAllTasks() => [.. _runtimeTasks.Values];

    public TaskInfo[] GetTasksByCategory(TaskCategory category) =>
        _runtimeTasks.Values.Where(t => t.Category == category).ToArray();

}

internal sealed class StrategyState
{
    private readonly ConcurrentDictionary<string, Strategy> _strategies = new();

    public bool AddOrUpdateStrategy(string key, Strategy value)
    {
        var isNew = !_strategies.ContainsKey(key);
        _strategies.AddOrUpdate(key, value, (_, _) => value);
        return isNew;
    }

    public bool TryRemoveStrategy(string key, out Strategy? value)
    {
        var result = _strategies.TryRemove(key, out value);
        return result;
    }

    public bool TryGetStrategy(string key, out Strategy? value) =>
        _strategies.TryGetValue(key, out value);

    public Strategy? GetStrategyById(string strategyId) =>
        _strategies.TryGetValue(strategyId, out var strategy) ? strategy : null;

    public Strategy[] GetAllStrategies() => [.. _strategies.Values];

}

public class GlobalState
{
    private readonly AlertState _alerts;
    private readonly KlineState _klines;
    private readonly StreamState _stream;
    private readonly TaskState _taskState;
    private readonly StrategyState _strategies;

    public GlobalState(ILogger<GlobalState> logger)
    {
        _alerts = new AlertState();
        _klines = new KlineState();
        _stream = new StreamState();
        _taskState = new TaskState();
        _strategies = new StrategyState();
        logger.LogInformation("Global state initialized.");
    }

    #region Delegates to Sub-States
    public virtual bool AddOrUpdateAlert(string id, Alert alert) => _alerts.AddOrUpdate(id, alert);
    public virtual bool TryGetAlert(string id, out Alert? alert) => _alerts.TryGetValue(id, out alert);
    public virtual bool TryRemoveAlert(string id) => _alerts.TryRemove(id);
    public virtual void ClearAlerts() => _alerts.Clear();
    public virtual void AddOrUpdateLastKline(string key, IBinanceKline kline) => _klines.AddOrUpdate(key, kline);
    public virtual bool TryGetLastKline(string key, out IBinanceKline? kline) => _klines.TryGetValue(key, out kline);
    public virtual bool TryRemoveLastKline(string key) => _klines.TryRemove(key);
    public virtual void ClearLastKlines() => _klines.Clear();

    public virtual DateTime? LastConnectionTime { get => _stream.LastConnectionTime; set => _stream.LastConnectionTime = value; }
    public virtual UpdateSubscription? CurrentSubscription { get => _stream.CurrentSubscription; set => _stream.CurrentSubscription = value; }
    public virtual bool TryAddSymbols(IEnumerable<string> symbols) => _stream.TryAddSymbols(symbols);
    public virtual bool TryAddIntervals(IEnumerable<string> intervals) => _stream.TryAddIntervals(intervals);
    public virtual HashSet<string> GetAllSymbols() => _stream.GetAllSymbols();
    public virtual HashSet<string> GetAllIntervals() => _stream.GetAllIntervals();
    public virtual void ClearStreamState() => _stream.ClearStreamState();
    public virtual bool NeedsReconnection() => _stream.NeedsReconnection();

    public virtual bool TryAddTask(TaskInfo taskInfo) => _taskState.TryAddTask(taskInfo);
    public virtual bool TryRemoveTask(string taskId, out TaskInfo? taskInfo) => _taskState.TryRemoveTask(taskId, out taskInfo);
    public virtual bool TryGetTask(string taskId, out TaskInfo? taskInfo) => _taskState.TryGetTask(taskId, out taskInfo);
    public virtual TaskInfo[] GetAllTasks() => _taskState.GetAllTasks();

    public virtual bool AddOrUpdateStrategy(string key, Strategy value) => _strategies.AddOrUpdateStrategy(key, value);
    public virtual bool TryRemoveStrategy(string key, out Strategy? value) => _strategies.TryRemoveStrategy(key, out value);
    public virtual bool TryGetStrategy(string key, out Strategy? value) => _strategies.TryGetStrategy(key, out value);
    public virtual Strategy[] GetAllStrategies() => _strategies.GetAllStrategies();

    #endregion
}
