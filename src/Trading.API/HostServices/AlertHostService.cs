using Trading.Application.Services.Alerts;
using Trading.Application.Telegram.Logging;
using Trading.Domain.IRepositories;

namespace Trading.API.HostServices;

public class AlertHostService : BackgroundService
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAlertTaskManager _alertTaskManager;
    private readonly ILogger<AlertHostService> _logger;
    private bool _initialized;

    public AlertHostService(ILogger<AlertHostService> logger,
                            IAlertRepository alertRepository,
                            IAlertTaskManager alertTaskManager)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _alertTaskManager = alertTaskManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_initialized)
                {
                    var alerts = await _alertRepository.GetActiveAlertsAsync(stoppingToken);
                    foreach (var alert in alerts)
                    {
                        await _alertTaskManager.StartAsync(alert, stoppingToken);
                    }
                    _initialized = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogErrorNotification(ex, "Error initializing alertHost service");
            }
            await SimulateDelay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public virtual Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
