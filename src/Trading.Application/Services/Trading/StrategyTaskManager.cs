using Microsoft.Extensions.Logging;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Trading;

public interface IStrategyTaskManager
{
    Task PauseAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task StartAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task StopAsync(Strategy strategy, CancellationToken cancellationToken = default);
    Task ResumeAsync(Strategy strategy, CancellationToken cancellationToken = default);

}

public class StrategyTaskManager : IStrategyTaskManager
{
    private readonly ILogger<StrategyTaskManager> _logger;
    private readonly ITaskManager _baseTaskManager;
    private readonly GlobalState _globalState;
    private readonly IAccountProcessorFactory _accountProcessorFactory;
    private readonly IExecutorFactory _executorFactory;

    public StrategyTaskManager(
        ILogger<StrategyTaskManager> logger,
        ITaskManager baseTaskManager,
        GlobalState globalState,
        IAccountProcessorFactory accountProcessorFactory,
        IExecutorFactory executorFactory)
    {
        _logger = logger;
        _baseTaskManager = baseTaskManager;
        _globalState = globalState;
        _accountProcessorFactory = accountProcessorFactory;
        _executorFactory = executorFactory;
    }

    public virtual async Task StartAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_globalState.AddOrUpdateStrategy(strategy.Id, strategy))
        {
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType)!;

            await _baseTaskManager.StartAsync(TaskCategory.Strategy,
                                          strategy.Id,
                                          async (ct) => await executor!.ExecuteLoopAsync(accountProcessor, strategy.Id, ct),
                                          cancellationToken);
        }
    }

    public async Task StopAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_globalState.TryRemoveStrategy(strategy.Id, out var _))
        {
            if (strategy.OrderId.HasValue)
            {
                var executor = _executorFactory.GetExecutor(strategy.StrategyType);
                var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType);
                await executor!.CancelExistingOrder(accountProcessor!, strategy, cancellationToken);
            }
            await _baseTaskManager.StopAsync(TaskCategory.Strategy, strategy.Id);
        }
    }

    public virtual async Task PauseAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_globalState.TryRemoveStrategy(strategy.Id, out var _))
        {
            await _baseTaskManager.StopAsync(TaskCategory.Strategy, strategy.Id);
        }
    }

    public async Task ResumeAsync(Strategy strategy, CancellationToken cancellationToken = default)
    {
        if (_globalState.AddOrUpdateStrategy(strategy.Id, strategy))
        {
            var executor = _executorFactory.GetExecutor(strategy.StrategyType);
            var accountProcessor = _accountProcessorFactory.GetAccountProcessor(strategy.AccountType)!;

            await _baseTaskManager.StartAsync(TaskCategory.Strategy,
                                          strategy.Id,
                                          async (ct) => await executor!.ExecuteLoopAsync(accountProcessor, strategy.Id, ct),
                                          cancellationToken);
        }
    }
}
