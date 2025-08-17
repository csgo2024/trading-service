using MediatR;
using Trading.Domain.Events;

namespace Trading.Application.Services.Alerts;

public interface IKlineStreamEventHandler :
    INotificationHandler<AlertResumedEvent>,
    INotificationHandler<AlertCreatedEvent>,
    INotificationHandler<StrategyCreatedEvent>
{
}

public class KlineStreamEventHandler : IKlineStreamEventHandler
{
    private readonly IKlineStreamManager _klineStreamManager;

    public KlineStreamEventHandler(IKlineStreamManager klineStreamManager)
    {
        _klineStreamManager = klineStreamManager;
    }

    public async Task Handle(AlertResumedEvent notification, CancellationToken cancellationToken)
    {
        await _klineStreamManager.SubscribeSymbols([notification.Alert.Symbol], [notification.Alert.Interval], cancellationToken);
    }

    public async Task Handle(AlertCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _klineStreamManager.SubscribeSymbols([notification.Alert.Symbol], [notification.Alert.Interval], cancellationToken);
    }

    public async Task Handle(StrategyCreatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.Strategy.Symbol != null && notification.Strategy.Interval != null)
        {
            await _klineStreamManager.SubscribeSymbols([notification.Strategy.Symbol], [notification.Strategy.Interval], cancellationToken);
        }
    }
}
