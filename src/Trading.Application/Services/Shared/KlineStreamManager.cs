using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.IntegrationEvents.Events;
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
        _logger.LogDebug("KlineStreamManager created : {HashCode}", GetHashCode());
    }

    public async Task<bool> SubscribeSymbols(HashSet<string> symbols, HashSet<string> intervals, CancellationToken ct)
    {
        if (symbols.Count == 0 || intervals.Count == 0)
        {
            return false;
        }

        await CloseExistingSubscription();

        var mergedSymbols = _globalState.GetAllSymbols();
        mergedSymbols.UnionWith(symbols);
        var mergedIntervals = _globalState.GetAllIntervals();
        mergedIntervals.UnionWith(intervals);

        var result = await _usdFutureSocketClient.ExchangeData.SubscribeToKlineUpdatesAsync(
            mergedSymbols,
            mergedIntervals.Select(BinanceHelper.ConvertToKlineInterval),
            HandlePriceUpdate,
            ct: ct);

        if (!result.Success)
        {
            _logger.LogError("Failed to subscribe: {@Error}", result.Error);
            return false;
        }

        _globalState.TryAddSymbols(mergedSymbols);
        _globalState.TryAddIntervals(mergedIntervals);
        _globalState.CurrentSubscription = result.Data;
        _globalState.LastConnectionTime = DateTime.UtcNow;

        _logger.LogInformation("Subscribed to {Count} symbols: {@Symbols} intervals: {@Intervals}",
                               mergedSymbols.Count,
                               mergedSymbols,
                               mergedIntervals);
        return true;
    }

    private void HandlePriceUpdate(DataEvent<IBinanceStreamKlineData> data)
    {
        if (!data.Data.Data.Final)
        {
            return;
        }

        Task.Run(() => _mediator.Publish(
            new KlineClosedEvent(data.Data.Symbol, data.Data.Data.Interval, data.Data.Data)
        ));
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
                _logger.LogError(ex, "Error closing subscription");
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
