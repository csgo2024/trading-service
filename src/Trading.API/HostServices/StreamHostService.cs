using Microsoft.Extensions.Localization;
using Trading.Application.Services.Shared;
using Trading.Application.Telegram.Logging;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class StreamHostService : BackgroundService
{
    private readonly IAlertRepository _alertRepository;
    private readonly IKlineStreamManager _klineStreamManager;
    private readonly ILogger<StreamHostService> _logger;
    private readonly IStringLocalizer<StreamHostService> _localizer;
    private readonly IStrategyRepository _strategyRepository;

    private bool _initial = true;
    private bool _isSubscribed;

    public StreamHostService(ILogger<StreamHostService> logger,
                             IAlertRepository alertRepository,
                             IStrategyRepository strategyRepository,
                             IKlineStreamManager klineStreamManager,
                             IStringLocalizer<StreamHostService> localizer)
    {
        _alertRepository = alertRepository;
        _klineStreamManager = klineStreamManager;
        _logger = logger;
        _localizer = localizer;
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
                else
                {
                    await SimulateDelay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation exceptions
            }
            catch (Exception ex)
            {
                _isSubscribed = false;
                var errorMessage = _initial
                    ? _localizer["InitialSubscriptionFailedWithRetry"]
                    : _localizer["ReconnectionFailedWithRetry"];
                _logger.LogErrorNotification(ex, errorMessage, 10);
                await SimulateDelay(TimeSpan.FromSeconds(10), stoppingToken);
            }
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
                _logger.LogInfoNotification(_localizer["InitialSubscriptionCompleted"]);
            }
            else
            {
                _logger.LogInfoNotification(_localizer["ReconnectionCompleted"]);
            }
        }

        return _isSubscribed;
    }

    private async Task WaitForNextReconnection(CancellationToken stoppingToken)
    {
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
