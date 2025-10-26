using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects.Models;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Common.Helpers;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public abstract class BaseExecutor
{
    protected readonly ILogger _logger;
    protected readonly IStrategyRepository _strategyRepository;
    protected readonly JavaScriptEvaluator _javaScriptEvaluator;
    protected readonly GlobalState _globalState;
    protected readonly IAccountProcessorFactory _accountProcessorFactory;
    public BaseExecutor(ILogger logger,
                        IStrategyRepository strategyRepository,
                        JavaScriptEvaluator javaScriptEvaluator,
                        IAccountProcessorFactory accountProcessorFactory,
                        GlobalState globalState)
    {
        _strategyRepository = strategyRepository;
        _javaScriptEvaluator = javaScriptEvaluator;
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _globalState = globalState;
    }

    private void Log(LogLevel level, Strategy strategy, bool disableNotification, string? message, params object?[] args)
    {
        var title = $"📊 {strategy.AccountType}-{strategy.Symbol}-{strategy.Interval}-{strategy.StrategyType}";

        message = $"""
                   {TelegramLogger.GetEmoji(level)} {level} ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss})
                   {message}
                   """;
        _logger.LogNotification(level, title, disableNotification, null, message, args);
    }

    public abstract StrategyType StrategyType { get; }
    private static Task<WebCallResult<BinanceOrderBase>> PlaceOrderAsync(IAccountProcessor accountProcessor,
                                                                         Strategy strategy,
                                                                         CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.OpenSell || strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.PlaceShortOrderAsync(strategy.Symbol, strategy.Quantity, strategy.TargetPrice, TimeInForce.GoodTillCanceled, ct);
        }
        return accountProcessor.PlaceLongOrderAsync(strategy.Symbol, strategy.Quantity, strategy.TargetPrice, TimeInForce.GoodTillCanceled, ct);
    }
    private static Task<WebCallResult<BinanceOrderBase>> StopOrderAsync(IAccountProcessor accountProcessor,
                                                                        Strategy strategy,
                                                                        decimal price,
                                                                        CancellationToken ct)
    {
        if (strategy.StrategyType == StrategyType.OpenSell || strategy.StrategyType == StrategyType.CloseSell)
        {
            return accountProcessor.StopShortOrderAsync(strategy.Symbol, strategy.Quantity, price, ct);
        }
        return accountProcessor.StopLongOrderAsync(strategy.Symbol, strategy.Quantity, price, ct);
    }
    public virtual async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        await CheckOrderStatus(accountProcessor, strategy, ct);
        strategy.UpdatedAt = DateTime.Now;
        await _strategyRepository.UpdateAsync(strategy.Id, strategy, ct);
    }

    public virtual async Task ExecuteLoopAsync(IAccountProcessor accountProcessor, string strategyId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // get strategy from global state to ensure we have the latest state
                _globalState.TryGetStrategy(strategyId, out var strategy);
                if (strategy != null)
                {
                    await ExecuteAsync(accountProcessor, strategy, cancellationToken);
                    // after ExecuteAsync is called, orderPlacedTime has been updated
                    // so we need to update strategy in global state to ensure we don't execute too frequently
                    _globalState.AddOrUpdateStrategy(strategyId, strategy);
                }
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogErrorNotification(ex, "Error executing strategy {StrategyId}", strategyId);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    public virtual async Task CancelExistingOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        if (strategy.OrderId is null)
        {
            return;
        }
        if (strategy.OrderPlacedTime.HasValue && strategy.HasOpenOrder)
        {
            var cancelResult = await accountProcessor.CancelOrderAsync(strategy.Symbol, strategy.OrderId.Value, ct);
            if (cancelResult.Success)
            {
                Log(LogLevel.Information, strategy, true, "Order cancelled successfully, OrderId={OrderId}", strategy.OrderId);
                strategy.HasOpenOrder = false;
                strategy.OrderId = null;
                strategy.OrderPlacedTime = null;
                return;
            }
            Log(LogLevel.Error, strategy, false, "Failed to cancel order. Error: {error}", cancelResult.Error?.ErrorDescription);
        }

    }
    public virtual async Task TryStopOrderAsync(IAccountProcessor accountProcessor, Strategy strategy, decimal stopPrice, CancellationToken ct)
    {
        if (strategy.OrderId is null)
        {
            return;
        }
        var result = await StopOrderAsync(accountProcessor, strategy, stopPrice, ct);
        if (result.Success)
        {
            Log(LogLevel.Information, strategy, false, "Triggering stop loss at price {Price}", stopPrice);
            strategy.OrderId = null;
            strategy.OrderPlacedTime = null;
            strategy.HasOpenOrder = false;
            return;
        }
        Log(LogLevel.Error, strategy, false, "Failed to stop order: {OrderId} at price: {Price}, Error: {ErrorMessage}", strategy.OrderId, stopPrice, result.Error?.Message);
    }
    public virtual async Task CheckOrderStatus(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        // NO open order skip check.
        if (!strategy.HasOpenOrder || strategy.OrderId is null)
        {
            strategy.HasOpenOrder = false;
            return;
        }

        var orderResult = await accountProcessor.GetOrder(strategy.Symbol, strategy.OrderId.Value, ct);
        if (orderResult.Success)
        {
            switch (orderResult.Data.Status)
            {
                case OrderStatus.Filled:
                    Log(LogLevel.Information, strategy, false, "Order filled successfully at price={Price}", strategy.TargetPrice);

                    strategy.HasOpenOrder = false;
                    // Once Order filled, replace executed quantity of the order.
                    strategy.Quantity = orderResult.Data.QuantityFilled;
                    break;

                case OrderStatus.Canceled:
                case OrderStatus.Expired:
                case OrderStatus.Rejected:
                    Log(LogLevel.Information, strategy, true, "Order {Status}. Will try to place new.", orderResult.Data.Status);

                    strategy.HasOpenOrder = false;
                    strategy.OrderId = null;
                    strategy.OrderPlacedTime = null;
                    break;
            }
            return;
        }

        Log(LogLevel.Error, strategy, false, "Failed to check order status, Error: {ErrorMessage}", orderResult.Error?.ErrorDescription);
    }
    public virtual async Task TryPlaceOrder(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        // OrderId is not null, no need to place order.
        if (strategy.OrderId is not null)
        {
            return;
        }
        var quantity = CommonHelper.TrimEndZero(strategy.Quantity);
        var price = CommonHelper.TrimEndZero(strategy.TargetPrice);
        strategy.TargetPrice = price;
        strategy.Quantity = quantity;
        // always set order placed time when placing order.
        strategy.OrderPlacedTime = DateTime.UtcNow;

        var result = await PlaceOrderAsync(accountProcessor, strategy, ct);
        if (result.Success)
        {
            Log(LogLevel.Information, strategy, true, "Order placed at price {Price}, quantity: {Quantity}", price, quantity);
            strategy.OrderId = result.Data.Id;
            strategy.HasOpenOrder = true;
            strategy.UpdatedAt = DateTime.UtcNow;
            return;
        }
        Log(LogLevel.Error, strategy, false,
            "Failed to place order at price: {Price}, quantity: {Quantity}. Error: {ErrorMessage}", strategy.TargetPrice,
            quantity, result?.Error?.ErrorDescription);
    }

    public virtual Task HandleKlineClosedEvent(IAccountProcessor accountProcessor, Strategy strategy, KlineClosedEvent @event, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
    public virtual bool ShouldStopLoss(Strategy strategy, KlineClosedEvent @event)
    {
        if (string.IsNullOrEmpty(strategy.StopLossExpression))
        {
            return false;
        }
        return _javaScriptEvaluator.EvaluateExpression(strategy.StopLossExpression,
                                                       @event.Kline.OpenPrice,
                                                       @event.Kline.ClosePrice,
                                                       @event.Kline.HighPrice,
                                                       @event.Kline.LowPrice);
    }
    public virtual async Task<IBinanceKline?> FetchLatestKline(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        var klineInterval = BinanceHelper.ConvertToKlineInterval(strategy.Interval!);
        var kLines = await accountProcessor.GetKlines(strategy.Symbol, klineInterval, limit: 1, ct: ct);

        if (!kLines.Success || kLines.Data.Length == 0)
        {
            Log(LogLevel.Error, strategy, false, "Failed to get latest Kline, Error: {ErrorMessage}", kLines.Error?.Message);
            return null;
        }

        return kLines.Data.First();
    }
    public virtual bool ShouldFetchLatestKline(Strategy strategy)
    {
        if (strategy.OpenPrice is null || strategy.TargetPrice <= 0 || strategy.Quantity <= 0)
        {
            return true;
        }
        if (!strategy.OrderPlacedTime.HasValue)
        {
            return true;
        }
        var (_, close) = BinanceHelper.GetKLinePeriod(strategy.OrderPlacedTime.Value, strategy.Interval!);
        if (DateTime.UtcNow > close && strategy.AutoReset)
        {
            Log(LogLevel.Information, strategy, true, "New {Interval}-interval started, fetching latest kline and will try to cancel previous order if any.", strategy.Interval);
            return true;
        }
        return false;
    }
}
