using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Shared;

public class ConcurrentStateBase<TKey, TValue> where TKey : notnull
{
    protected readonly ConcurrentDictionary<TKey, TValue> _state = new();

    public virtual bool TryAdd(TKey key, TValue value) =>
        _state.TryAdd(key, value);

    public virtual bool AddOrUpdate(TKey key, TValue value)
    {
        var isNew = !_state.ContainsKey(key);
        _state.AddOrUpdate(key, value, (_, _) => value);
        return isNew;
    }

    public virtual bool TryGetValue(TKey key, out TValue? value) =>
        _state.TryGetValue(key, out value);

    public virtual bool TryRemove(TKey key, out TValue? value) =>
        _state.TryRemove(key, out value);

    public virtual void Clear() => _state.Clear();

    public virtual TValue[] Values() => [.. _state.Values];
}

internal sealed class StreamState
{
    private readonly HashSet<string> _symbols = new();
    private readonly HashSet<string> _intervals = new();
    private readonly TimeSpan _reconnectInterval = TimeSpan.FromHours(12);

    public DateTime? LastConnectionTime { get; set; }
    public UpdateSubscription? CurrentSubscription { get; set; }

    public bool TryAddSymbols(IEnumerable<string> symbols)
    {
        var before = _symbols.Count;
        _symbols.UnionWith(symbols);
        return _symbols.Count > before;
    }

    public bool TryAddIntervals(IEnumerable<string> intervals)
    {
        var before = _intervals.Count;
        _intervals.UnionWith(intervals);
        return _intervals.Count > before;
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

    public bool NeedsReconnection() =>
        LastConnectionTime == null || (DateTime.UtcNow - LastConnectionTime.Value) > _reconnectInterval;
}

public class GlobalState
{
    private readonly ConcurrentStateBase<string, Alert> _alerts;
    private readonly ConcurrentStateBase<string, IBinanceKline> _klines;
    private readonly ConcurrentStateBase<string, Strategy> _strategies;
    private readonly ConcurrentStateBase<string, TaskInfo> _taskState;
    private readonly StreamState _stream;
    private readonly JsonSerializerOptions _options;

    public GlobalState(ILogger<GlobalState> logger)
    {
        _alerts = new ConcurrentStateBase<string, Alert>();
        _klines = new ConcurrentStateBase<string, IBinanceKline>();
        _strategies = new ConcurrentStateBase<string, Strategy>();
        _stream = new StreamState();
        _taskState = new ConcurrentStateBase<string, TaskInfo>();
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        logger.LogDebug("GlobalState created : {HashCode}", GetHashCode());
    }

    #region Delegates to Sub-States
    public virtual bool AddOrUpdateAlert(string id, Alert alert) => _alerts.AddOrUpdate(id, alert);
    public virtual bool TryGetAlert(string id, out Alert? alert) => _alerts.TryGetValue(id, out alert);
    public virtual bool TryRemoveAlert(string id) => _alerts.TryRemove(id, out _);
    public virtual void ClearAlerts() => _alerts.Clear();

    public virtual void AddOrUpdateLastKline(string key, IBinanceKline kline) => _klines.AddOrUpdate(key, kline);
    public virtual bool TryGetLastKline(string key, out IBinanceKline? kline) => _klines.TryGetValue(key, out kline);
    public virtual bool TryRemoveLastKline(string key) => _klines.TryRemove(key, out _);
    public virtual void ClearLastKlines() => _klines.Clear();

    public virtual DateTime? LastConnectionTime { get => _stream.LastConnectionTime; set => _stream.LastConnectionTime = value; }
    public virtual UpdateSubscription? CurrentSubscription { get => _stream.CurrentSubscription; set => _stream.CurrentSubscription = value; }
    public virtual bool TryAddSymbols(IEnumerable<string> symbols) => _stream.TryAddSymbols(symbols);
    public virtual bool TryAddIntervals(IEnumerable<string> intervals) => _stream.TryAddIntervals(intervals);
    public virtual HashSet<string> GetAllSymbols() => _stream.GetAllSymbols();
    public virtual HashSet<string> GetAllIntervals() => _stream.GetAllIntervals();
    public virtual void ClearStreamState() => _stream.ClearStreamState();
    public virtual bool NeedsReconnection() => _stream.NeedsReconnection();

    public virtual bool TryAddTask(TaskInfo taskInfo) => _taskState.TryAdd(taskInfo.Id, taskInfo);
    public virtual bool TryRemoveTask(string taskId, out TaskInfo? taskInfo) => _taskState.TryRemove(taskId, out taskInfo);
    public virtual bool TryGetTask(string taskId, out TaskInfo? taskInfo) => _taskState.TryGetValue(taskId, out taskInfo);
    public virtual TaskInfo[] GetAllTasks() => _taskState.Values();

    public virtual bool AddOrUpdateStrategy(string key, Strategy value) => _strategies.AddOrUpdate(key, value);
    public virtual bool TryRemoveStrategy(string key, out Strategy? value) => _strategies.TryRemove(key, out value);
    public virtual bool TryGetStrategy(string key, out Strategy? value) => _strategies.TryGetValue(key, out value);
    public virtual Strategy[] GetAllStrategies() => _strategies.Values();
    #endregion

    public override string ToString()
    {
        var snapshot = new
        {
            Alerts = _alerts.Values().Select(x => new { x.Id, x.Expression, x.Interval }),
            Strategies = _strategies.Values().Select(x => new { x.Id, Name = $"{x.Interval}-{x.AccountType}-{x.StrategyType}" }),
            Symbols = _stream.GetAllSymbols(),
            Intervals = _stream.GetAllIntervals(),
            Tasks = _taskState.Values().Select(x => new { x.Id, x.Category }),
            ConnectTime = _stream.LastConnectionTime.HasValue ? _stream.LastConnectionTime.Value.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") : "N/A",
        };
        return JsonSerializer.Serialize(snapshot, _options);
    }

}
