using Trading.Application.Services.Trading;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class TradingHostService : BackgroundService
{
    private readonly ILogger<TradingHostService> _logger;
    private readonly IStrategyRepository _strategyRepository;

    private readonly IStrategyTaskManager _strategyTaskManager;

    public TradingHostService(ILogger<TradingHostService> logger,
                              IStrategyRepository strategyRepository,
                              IStrategyTaskManager strategyTaskManager)
    {
        _logger = logger;
        _strategyRepository = strategyRepository;
        _strategyTaskManager = strategyTaskManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var strategies = await _strategyRepository.GetActiveStrategyAsync(stoppingToken);
                foreach (var strategy in strategies)
                {
                    await _strategyTaskManager.HandleCreatedAsync(strategy, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing trading service");
            }
            await SimulateDelay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
