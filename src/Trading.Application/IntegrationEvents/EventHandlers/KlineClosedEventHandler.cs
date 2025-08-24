using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.IntegrationEvents.EventHandlers;

public class KlineClosedEventHandler : INotificationHandler<KlineClosedEvent>
{
    private readonly ILogger<KlineClosedEventHandler> _logger;
    private readonly IExecutorFactory _executorFactory;
    private readonly IStrategyRepository _strategyRepository;
    private readonly GlobalState _globalState;
    private readonly IAccountProcessorFactory _accountProcessorFactory;

    public KlineClosedEventHandler(
        ILogger<KlineClosedEventHandler> logger,
        IStrategyRepository strategyRepository,
        IAccountProcessorFactory accountProcessorFactory,
        GlobalState globalState,
        IExecutorFactory executorFactory)
    {
        _strategyRepository = strategyRepository;
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _globalState = globalState;
        _executorFactory = executorFactory;
    }

    public virtual async Task Handle(KlineClosedEvent @event, CancellationToken cancellationToken)
    {
        var strategies = _globalState.GetAllStrategies().Where(x => x.Symbol == @event.Symbol
            && x.Interval == BinanceHelper.ConvertToIntervalString(@event.Interval));
        var tasks = strategies.Select(async strategy =>
        {
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            if (executor != null && accountProcessor != null)
            {
                await executor.HandleKlineClosedEvent(accountProcessor, strategy, @event, cancellationToken);
                if (executor.ShouldStopLoss(strategy, @event))
                {
                    await executor.TryStopOrderAsync(accountProcessor, strategy, @event.Kline.ClosePrice, cancellationToken);
                    strategy.Pause();
                }
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
                _globalState.AddOrUpdateStrategy(strategy.Id, strategy); // 更新内存中的策略状态
            }
        });
        await Task.WhenAll(tasks);
    }
}
