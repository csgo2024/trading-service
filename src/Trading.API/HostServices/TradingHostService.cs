using Trading.Application.Services.Trading;
using Trading.Application.Telegram.Logging;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class TradingHostService : BackgroundService
{
    private readonly ILogger<TradingHostService> _logger;
    private readonly IStrategyRepository _strategyRepository;
    private readonly IStrategyTaskManager _strategyTaskManager;
    private bool _initialized;

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
                if (!_initialized)
                {
                    var strategies = await _strategyRepository.GetActiveStrategyAsync(stoppingToken);
                    foreach (var strategy in strategies)
                    {
                        await _strategyTaskManager.StartAsync(strategy, stoppingToken);
                    }
                    _initialized = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorNotification(ex, "Error initializing trading service");
            }
            await SimulateDelay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
