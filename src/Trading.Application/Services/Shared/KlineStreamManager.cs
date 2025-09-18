using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Telegram.Logging;
using Trading.Exchange.Binance.Helpers;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Services.Shared;

public interface IKlineStreamManager
{
    Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct, bool force = false);
    DateTime GetNextReconnectTime(DateTime dateTime);
}

public class KlineStreamManager : IKlineStreamManager, IAsyncDisposable
{
    private readonly BinanceSocketClientUsdFuturesApiWrapper _usdFutureSocketClient;
    private readonly ILogger<KlineStreamManager> _logger;
    private readonly IMediator _mediator;
    private readonly GlobalState _globalState;

    private readonly Action<TimeSpan> _connectionRestoredHandler;
    private readonly Action _connectionLostHandler;
    private readonly Action<Error> _resubscribingFailedHandler;

    private bool _disposed;

    public KlineStreamManager(
        ILogger<KlineStreamManager> logger,
        IMediator mediator,
        BinanceSocketClientUsdFuturesApiWrapper usdFutureSocketClient,
        GlobalState globalState)
    {
        _logger = logger;
        _mediator = mediator;
        _usdFutureSocketClient = usdFutureSocketClient;
        _globalState = globalState;

        _connectionRestoredHandler = (period) => _logger.LogInformation("Connection restored successfully.");
        _connectionLostHandler = () => _logger.LogWarning("Connection lost for subscription");
        _resubscribingFailedHandler = (ex) => _logger.LogErrorNotification("Resubscription failed, Error: {@Error}", ex);
        _logger.LogInformation("KlineStreamManager created : {HashCode}", GetHashCode());
    }

    public async Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct, bool force = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(KlineStreamManager));
        if (symbols.Count == 0 || intervals.Count == 0)
        {
            return false;
        }
        var hasChanges = HasNewSymbolsOrIntervals(symbols, intervals, out var mergedSymbols, out var mergedIntervals);

        // subscribe only if there are new symbols/intervals or if forced
        if (hasChanges || force)
        {
            await CloseExistingSubscription();

            var result = await _usdFutureSocketClient.ExchangeData.SubscribeToKlineUpdatesAsync(
                mergedSymbols,
                mergedIntervals.Select(BinanceHelper.ConvertToKlineInterval),
                HandlePriceUpdate,
                ct: ct);

            if (!result.Success)
            {
                _logger.LogErrorNotification("Failed to subscribe: {@Error}", result.Error);
                return false;
            }

            RegisterSubscriptionLifecycleEvents(result.Data);

            _globalState.TryAddSymbols(mergedSymbols);
            _globalState.TryAddIntervals(mergedIntervals);
            _globalState.CurrentSubscription = result.Data;
            _globalState.LastConnectionTime = DateTime.UtcNow;

            _logger.LogInfoNotification("Subscribed to {Count} symbols: {@Symbols} intervals: {@Intervals}",
                                   mergedSymbols.Count,
                                   mergedSymbols,
                                   mergedIntervals);
        }
        return true;
    }

    private void RegisterSubscriptionLifecycleEvents(UpdateSubscription? subscription)
    {
        if (subscription == null)
        {
            return;
        }
        subscription.ConnectionRestored += _connectionRestoredHandler;
        subscription.ConnectionLost += _connectionLostHandler;
        subscription.ResubscribingFailed += _resubscribingFailedHandler;
    }

    private void UnregisterSubscriptionLifecycleEvents(UpdateSubscription? subscription)
    {
        if (subscription == null)
        {
            return;
        }
        subscription.ConnectionRestored -= _connectionRestoredHandler;
        subscription.ConnectionLost -= _connectionLostHandler;
        subscription.ResubscribingFailed -= _resubscribingFailedHandler;
    }
    private bool HasNewSymbolsOrIntervals(
        HashSet<string> symbols,
        HashSet<string> intervals,
        out HashSet<string> mergedSymbols,
        out HashSet<string> mergedIntervals)
    {
        var existingSymbols = _globalState.GetAllSymbols();
        var existingIntervals = _globalState.GetAllIntervals();

        var newSymbols = symbols.Except(existingSymbols).ToHashSet();
        var newIntervals = intervals.Except(existingIntervals).ToHashSet();

        // 合并后的最终集合
        mergedSymbols = [.. existingSymbols];
        mergedSymbols.UnionWith(newSymbols);

        mergedIntervals = [.. existingIntervals];
        mergedIntervals.UnionWith(newIntervals);

        // 只要有新增就返回 true
        return newSymbols.Count > 0 || newIntervals.Count > 0;
    }
    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }
        // fire-and-forget Task
        _ = _mediator.Publish(new KlineClosedEvent(data.Data.Symbol, data.Data.Data.Interval, data.Data.Data));
    }

    private async Task CloseExistingSubscription()
    {
        var subscription = _globalState.CurrentSubscription;
        if (subscription != null)
        {
            try
            {
                UnregisterSubscriptionLifecycleEvents(subscription);

                await subscription.CloseAsync();
                _globalState.CurrentSubscription = null;
            }
            catch (Exception ex)
            {
                _logger.LogErrorNotification(ex, "Error closing subscription");
            }
        }
    }

    public DateTime GetNextReconnectTime(DateTime dateTime) => _globalState.NextReconnectTime(dateTime);

    public async ValueTask DisposeAsync()
    {
        // 防止重复释放
        if (_disposed)
        {
            return;
        }
        _logger.LogInformation("Disposing KlineStreamManager: {HashCode}", GetHashCode());

        var subscription = _globalState.CurrentSubscription;
        if (subscription != null)
        {
            UnregisterSubscriptionLifecycleEvents(subscription);
            try
            {
                await subscription.CloseAsync();
            }
            catch (Exception)
            {
                // 释放资源的操作不应该抛出异常
            }
        }
        _globalState.ClearStreamState();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

}
