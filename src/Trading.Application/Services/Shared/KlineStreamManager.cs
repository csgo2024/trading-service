using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Telegram.Logging;
using Trading.Exchange.Binance.Helpers;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Services.Shared;

public interface IKlineStreamManager : IDisposable
{
    Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct);
    bool NeedsReconnection();
}

public class KlineStreamManager : IKlineStreamManager
{
    private readonly BinanceSocketClientUsdFuturesApiWrapper _usdFutureSocketClient;
    private readonly ILogger<KlineStreamManager> _logger;
    private readonly IMediator _mediator;
    private readonly GlobalState _globalState;
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
        _logger.LogInformation("KlineStreamManager created : {HashCode}", GetHashCode());
    }

    public async Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct)
    {
        if (symbols.Count == 0 || intervals.Count == 0)
        {
            return false;
        }
        var hasChanges = HasNewSymbolsOrIntervals(symbols, intervals, out var mergedSymbols, out var mergedIntervals);

        // 只在有新 symbol/interval 或者 需要重新连接时才触发订阅
        if (hasChanges || NeedsReconnection())
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
                await subscription.CloseAsync();
                _globalState.CurrentSubscription = null;
            }
            catch (Exception ex)
            {
                _logger.LogErrorNotification(ex, "Error closing subscription");
            }
        }
    }

    public bool NeedsReconnection() => _globalState.NeedsReconnection();

    public void Dispose()
    {
        _globalState.CurrentSubscription?.CloseAsync().Wait();
        _globalState.ClearStreamState();
    }

}
