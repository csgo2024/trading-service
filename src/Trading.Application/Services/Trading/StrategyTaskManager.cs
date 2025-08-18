using Microsoft.Extensions.Logging;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading;

public interface IStrategyTaskManager
{
    Task HandlePausedAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task HandleCreatedAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task HandleDeletedAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task HandleResumedAsync(Strategy strategy, CancellationToken cancellationToken = default);

}

public class StrategyTaskManager : IStrategyTaskManager
{
    protected readonly ILogger<StrategyTaskManager> _logger;
    protected readonly IBackgroundTaskManager _backgroundTaskManager;
    protected readonly IStrategyState _strategyState;
    protected readonly IAccountProcessorFactory _accountProcessorFactory;
    protected readonly IExecutorFactory _executorFactory;

    public StrategyTaskManager(
        ILogger<StrategyTaskManager> logger,
        IBackgroundTaskManager backgroundTaskManager,
        IStrategyState strategyState,
        IAccountProcessorFactory accountProcessorFactory,
        IExecutorFactory executorFactory)
    {
        _logger = logger;
        _backgroundTaskManager = backgroundTaskManager;
        _strategyState = strategyState;
        _accountProcessorFactory = accountProcessorFactory;
        _executorFactory = executorFactory;
    }

    public virtual async Task HandleCreatedAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_strategyState.TryAdd(strategy.Id, strategy))
        {
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType)!;

            await _backgroundTaskManager.StartAsync(TaskCategory.Strategy,
                                          strategy.Id,
                                          async (ct) => await executor!.ExecuteLoopAsync(accountProcessor, strategy.Id, ct),
                                          cancellationToken);
            _logger.LogInformation("[{AccountType}-{Symbol}-{StrategyType}] Added strategy to monitoring list.",
                                   strategy.AccountType, strategy.Symbol, strategy.StrategyType);

        }
    }

    public async Task HandleDeletedAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_strategyState.TryRemove(strategy.Id, out var _))
        {
            if (strategy.OrderId.HasValue)
            {
                var executor = _executorFactory.GetExecutor(strategy.StrategyType);
                var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
                await executor!.CancelExistingOrder(accountProcessor!, strategy, cancellationToken);
            }
            await _backgroundTaskManager.StopAsync(TaskCategory.Strategy, strategy.Id);
            _logger.LogInformation("[{AccountType}-{Symbol}-{StrategyType}] Removed strategy from monitoring list.",
                                   strategy.AccountType, strategy.Symbol, strategy.StrategyType);
        }
    }

    public virtual async Task HandlePausedAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_strategyState.TryRemove(strategy.Id, out var _))
        {
            await _backgroundTaskManager.StopAsync(TaskCategory.Strategy, strategy.Id);
            _logger.LogInformation("[{AccountType}-{Symbol}-{StrategyType}] Removed strategy from monitoring list.",
                                   strategy.AccountType, strategy.Symbol, strategy.StrategyType);
        }
    }

    public async Task HandleResumedAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_strategyState.TryAdd(strategy.Id, strategy))
        {
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType)!;

            await _backgroundTaskManager.StartAsync(TaskCategory.Strategy,
                                          strategy.Id,
                                          async (ct) => await executor!.ExecuteLoopAsync(accountProcessor, strategy.Id, ct),
                                          cancellationToken);
            _logger.LogInformation("[{AccountType}-{Symbol}-{StrategyType}] Added strategy to monitoring list.",
                                   strategy.AccountType, strategy.Symbol, strategy.StrategyType);

        }
    }
}
