using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading;

public interface IStrategyState
{
    bool TryAdd(string key, Strategy value);
    bool TryRemove(string key, out Strategy? value);
    bool TryGetValue(string key, out Strategy? value);
    Strategy? GetStrategyById(string strategyId);
    Strategy[] All();
}

public class StrategyState : IStrategyState
{
    protected readonly ILogger _logger;
    protected readonly ConcurrentDictionary<string, Strategy> _states = new();

    public StrategyState(ILogger<StrategyState> logger)
    {
        _logger = logger;
        _logger.LogInformation("[StrategyState]HashCode: {HashCode}", GetHashCode());
    }
    public Strategy[] All() => [.. _states.Values];

    public Strategy? GetStrategyById(string strategyId)
    {
        _states.TryGetValue(strategyId, out var strategy);
        return strategy;
    }

    public bool TryAdd(string key, Strategy value)
    {
        return _states.TryAdd(key, value);
    }

    public bool TryGetValue(string key, out Strategy? value)
    {
        return _states.TryGetValue(key, out value);
    }

    public bool TryRemove(string key, out Strategy? value)
    {
        return _states.TryRemove(key, out value);
    }
}
