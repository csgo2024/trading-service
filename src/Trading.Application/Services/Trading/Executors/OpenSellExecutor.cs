using Microsoft.Extensions.Logging;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Common.Enums;
using Trading.Common.Helpers;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Trading.Executors;

public class OpenSellExecutor : BaseExecutor
{
    public OpenSellExecutor(ILogger<OpenSellExecutor> logger,
        IAccountProcessorFactory accountProcessorFactory,
        IStrategyRepository strategyRepository,
        JavaScriptEvaluator javaScriptEvaluator,
        GlobalState stateManager)
        : base(logger, strategyRepository, javaScriptEvaluator, accountProcessorFactory, stateManager)
    {
    }

    public override StrategyType StrategyType => StrategyType.OpenSell;

    public override async Task ExecuteAsync(IAccountProcessor accountProcessor, Strategy strategy, CancellationToken ct)
    {
        if (ShouldFetchLatestKline(strategy))
        {
            var currentKline = await FetchLatestKline(accountProcessor, strategy, ct);
            if (currentKline == null)
            {
                return;
            }
            if (strategy.OpenPrice is null || strategy.TargetPrice <= 0 || strategy.Quantity <= 0)
            {
                // First time setup
                await UpdateStrategyPricing(accountProcessor, strategy, currentKline.OpenPrice, ct);
            }
            if (strategy.AutoReset)
            {
                await CancelExistingOrder(accountProcessor, strategy, ct);
                await UpdateStrategyPricing(accountProcessor, strategy, currentKline.OpenPrice, ct);
            }
        }
        if (strategy.OrderId is null)
        {
            await TryPlaceOrder(accountProcessor, strategy, ct);
        }
        await base.ExecuteAsync(accountProcessor, strategy, ct);
    }
    private static async Task UpdateStrategyPricing(IAccountProcessor accountProcessor, Strategy strategy, decimal basePrice, CancellationToken ct)
    {
        var filterData = await accountProcessor.GetSymbolFilterData(strategy, ct);

        var adjustedOpenPrice = CommonHelper.TrimEndZero(basePrice);
        strategy.OpenPrice = adjustedOpenPrice;

        strategy.TargetPrice = BinanceHelper.AdjustPriceByStepSize(
            adjustedOpenPrice * (1 + strategy.Volatility), filterData.Item1);

        strategy.Quantity = BinanceHelper.AdjustQuantityBystepSize(
            strategy.Amount / strategy.TargetPrice, filterData.Item2);
    }
}
