using MediatR;
using Trading.Application.Services.Trading;
using Trading.Domain.Events;

namespace Trading.Application.DomainEventHandlers;

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
        => await _strategyTaskManager.StartAsync(notification.Strategy, cancellationToken);

    public async Task Handle(StrategyDeletedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.StopAsync(notification.Strategy, cancellationToken);

    public async Task Handle(StrategyPausedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.PauseAsync(notification.Strategy, cancellationToken);

    public async Task Handle(StrategyResumedEvent notification, CancellationToken cancellationToken)
        => await _strategyTaskManager.ResumeAsync(notification.Strategy, cancellationToken);

}
