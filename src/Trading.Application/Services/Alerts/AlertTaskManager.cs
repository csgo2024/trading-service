using Microsoft.Extensions.Logging;
using Trading.Application.Services.Shared;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Services.Alerts;

public interface IAlertTaskManager
{
    Task StartAsync(Alert alert, CancellationToken cancellationToken = default);
    Task StopAsync(Alert alert, CancellationToken cancellationToken = default);
    Task PauseAsync(Alert alert, CancellationToken cancellationToken = default);
    Task ResumeAsync(Alert alert, CancellationToken cancellationToken = default);
    Task EmptyAsync(CancellationToken cancellationToken);
}

public class AlertTaskManager : IAlertTaskManager
{
    private readonly GlobalState _globalState;
    private readonly ILogger<AlertTaskManager> _logger;
    private readonly IAlertNotificationService _alertNotificationService;
    private readonly ITaskManager _baseTaskManager;
    public AlertTaskManager(
        ILogger<AlertTaskManager> logger,
        ITaskManager baseTaskManager,
        IAlertNotificationService alertNotificationService,
        GlobalState globalState)
    {
        _alertNotificationService = alertNotificationService;
        _baseTaskManager = baseTaskManager;
        _globalState = globalState;
        _logger = logger;
    }

    public async Task StartAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _globalState.AddOrUpdateAlert(alert.Id, alert);
        await _baseTaskManager.StartAsync(TaskCategory.Alert,
                                          alert.Id,
                                          ct => _alertNotificationService.SendNotification(alert, ct),
                                          cancellationToken);
    }

    public async Task StopAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _globalState.TryRemoveAlert(alert.Id);
        _globalState.TryRemoveLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}");
        await _baseTaskManager.StopAsync(TaskCategory.Alert, alert.Id);
    }

    public async Task PauseAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _globalState.AddOrUpdateAlert(alert.Id, alert);
        _globalState.TryRemoveLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}");
        await _baseTaskManager.StopAsync(TaskCategory.Alert, alert.Id);
    }
    public async Task ResumeAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        _globalState.AddOrUpdateAlert(alert.Id, alert);
        await _baseTaskManager.StartAsync(TaskCategory.Alert,
                                          alert.Id,
                                          ct => _alertNotificationService.SendNotification(alert, ct),
                                          cancellationToken);
    }

    public async Task EmptyAsync(CancellationToken cancellationToken)
    {
        _globalState.ClearAlerts();
        _globalState.ClearLastKlines();
        _logger.LogInformation("Alerts emptyed, stopping all monitors.");
        await _baseTaskManager.StopAsync(TaskCategory.Alert);
    }
}
