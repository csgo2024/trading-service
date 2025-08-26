using Trading.Application.Services.Shared;
using Trading.Application.Telegram.Logging;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class StreamHostService : BackgroundService
{
    private readonly IAlertRepository _alertRepository;
    private readonly IKlineStreamManager _klineStreamManager;
    private readonly ILogger<StreamHostService> _logger;
    private readonly IStrategyRepository _strategyRepository;

    private bool _isSubscribed;

    public StreamHostService(ILogger<StreamHostService> logger,
                             IAlertRepository alertRepository,
                             IStrategyRepository strategyRepository,
                             IKlineStreamManager klineStreamManager)
    {
        _alertRepository = alertRepository;
        _klineStreamManager = klineStreamManager;
        _logger = logger;
        _strategyRepository = strategyRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 初次订阅
                if (!_isSubscribed)
                {
                    _isSubscribed = await SubscribeFromDatabase(stoppingToken);
                    if (_isSubscribed)
                    {
                        _logger.LogInfoNotification("Initial subscription completed successfully");
                    }
                }
                // Reconnect 时重新订阅
                else if (_klineStreamManager.NeedsReconnection())
                {
                    _isSubscribed = await SubscribeFromDatabase(stoppingToken);
                    if (_isSubscribed)
                    {
                        _logger.LogInfoNotification("Reconnection completed successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                var errorMessage = !_isSubscribed
                    ? "Initial subscription failed. Retrying in 1 minute..."
                    : "Reconnection failed. Retrying in 1 minute...";

                _logger.LogErrorNotification(ex, errorMessage);
            }
            await SimulateDelay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task<bool> SubscribeFromDatabase(CancellationToken stoppingToken)
    {
        var alerts = await _alertRepository.GetActiveAlertsAsync(stoppingToken);
        var strategies = await _strategyRepository.GetActiveStrategyAsync(stoppingToken);

        var symbols = alerts.Select(x => x.Symbol)
            .Concat(strategies.Select(x => x.Symbol))
            .ToHashSet();

        var intervals = alerts.Select(x => x.Interval)
            .Concat(strategies.Where(x => x.Interval != null).Select(x => x.Interval!))
            .ToHashSet();

        if (symbols.Count == 0 || intervals.Count == 0)
        {
            return false;
        }

        return await _klineStreamManager.SubscribeSymbols(symbols, intervals, stoppingToken);
    }

    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
