using MediatR;
using Trading.Domain.Events;

namespace Trading.Application.Services.Trading;

public class StrategyEventHandler :
    INotificationHandler<StrategyCreatedEvent>,
    INotificationHandler<StrategyDeletedEvent>,
    INotificationHandler<StrategyPausedEvent>,
    INotificationHandler<StrategyResumedEvent>
{
    private readonly IStrategyTaskManager _strategyTaskManager;

    public StrategyEventHandler(IStrategyTaskManager strategyTaskManager)
    {
        _strategyTaskManager = strategyTaskManager;
    }

    public async Task Handle(StrategyCreatedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.HandleCreatedAsync(notification.Strategy, cancellationToken);

    public async Task Handle(StrategyDeletedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.HandleDeletedAsync(notification.Strategy, cancellationToken);

    public async Task Handle(StrategyPausedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.HandlePausedAsync(notification.Strategy, cancellationToken);

    public async Task Handle(StrategyResumedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.HandleResumedAsync(notification.Strategy, cancellationToken);

}
