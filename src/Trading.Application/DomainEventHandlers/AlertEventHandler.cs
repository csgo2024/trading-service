using MediatR;
using Trading.Application.Services.Alerts;
using Trading.Domain.Events;

namespace Trading.Application.DomainEventHandlers;

public class AlertEventHandler :
    INotificationHandler<AlertCreatedEvent>,
    INotificationHandler<AlertDeletedEvent>,
    INotificationHandler<AlertPausedEvent>,
    INotificationHandler<AlertResumedEvent>,
    INotificationHandler<AlertEmptyedEvent>
{
    private readonly IAlertTaskManager _alertTaskManager;

    public AlertEventHandler(IAlertTaskManager alertTaskManager)
    {
        _alertTaskManager = alertTaskManager;
    }

    public async Task Handle(AlertCreatedEvent notification, CancellationToken cancellationToken)
        => await _alertTaskManager.StartAsync(notification.Alert, cancellationToken);

    public async Task Handle(AlertDeletedEvent notification, CancellationToken cancellationToken)
        => await _alertTaskManager.StopAsync(notification.Alert, cancellationToken);

    public async Task Handle(AlertPausedEvent notification, CancellationToken cancellationToken)
        => await _alertTaskManager.PauseAsync(notification.Alert, cancellationToken);

    public async Task Handle(AlertResumedEvent notification, CancellationToken cancellationToken)
        => await _alertTaskManager.ResumeAsync(notification.Alert, cancellationToken);

    public async Task Handle(AlertEmptyedEvent notification, CancellationToken cancellationToken)
        => await _alertTaskManager.EmptyAsync(cancellationToken);
}
