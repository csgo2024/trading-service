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

    private bool _initial = true;
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
                if (await TrySubscribe(stoppingToken))
                {
                    await WaitForNextReconnection(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _isSubscribed = false;
                var errorMessage = _initial
                    ? "Initial subscription failed. Retrying in 10 seconds..."
                    : "Reconnection failed. Retrying in 10 seconds...";
                _logger.LogErrorNotification(ex, errorMessage);
            }
            await SimulateDelay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task<bool> TrySubscribe(CancellationToken stoppingToken)
    {
        var wasInitial = _initial;
        _isSubscribed = await SubscribeFromDatabase(stoppingToken);

        if (_isSubscribed)
        {
            if (wasInitial)
            {
                _initial = false;
                _logger.LogInfoNotification("Initial subscription completed successfully");
            }
            else
            {
                _logger.LogInfoNotification("Reconnection completed successfully");
            }
        }

        return _isSubscribed;
    }

    private async Task WaitForNextReconnection(CancellationToken stoppingToken)
    {
        if (!_isSubscribed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var nextRun = _klineStreamManager.GetNextReconnectTime(now);
        var delay = nextRun - now;

        if (delay.TotalMilliseconds > 0)
        {
            await SimulateDelay(delay, stoppingToken);
        }
    }

    private async Task<bool> SubscribeFromDatabase(CancellationToken stoppingToken, bool force = true)
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

        return await _klineStreamManager.SubscribeSymbols(symbols, intervals, stoppingToken, force);
    }

    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
