using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.Services.Common;

public class KlineClosedEventHandler : INotificationHandler<KlineClosedEvent>
{
    private readonly ILogger<KlineClosedEventHandler> _logger;
    private readonly IExecutorFactory _executorFactory;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyState _strategyState;
    private readonly IAccountProcessorFactory _accountProcessorFactory;

    public KlineClosedEventHandler(
        ILogger<KlineClosedEventHandler> logger,
        IStrategyRepository strategyRepository,
        IAccountProcessorFactory accountProcessorFactory,
        IStrategyState strategyState,
        IExecutorFactory executorFactory)
    {
        _strategyRepository = strategyRepository;
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _strategyState = strategyState;
        _executorFactory = executorFactory;
    }

    public virtual async Task Handle(KlineClosedEvent @event, CancellationToken cancellationToken)
    {
        var strategies = _strategyState.All().Where(x => x.Symbol == @event.Symbol
            && x.Interval == BinanceHelper.ConvertToIntervalString(@event.Interval));
        var tasks = strategies.Select(async strategy =>
        {
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
            _logger.LogDebug("Handling KlineClosedEvent for strategy {Strategy} with executor {Executor}",
                JsonSerializer.Serialize(strategy), executor?.GetType().Name);
            if (executor != null && accountProcessor != null)
            {
                await executor.HandleKlineClosedEvent(accountProcessor, strategy, @event, cancellationToken);
                if (executor.ShouldStopLoss(strategy, @event))
                {
                    await executor.TryStopOrderAsync(accountProcessor, strategy, @event.Kline.ClosePrice, cancellationToken);
                    strategy.Pause();
                }
                await _strategyRepository.UpdateAsync(strategy.Id, strategy, cancellationToken);
            }
        });
        await Task.WhenAll(tasks);
    }
}
