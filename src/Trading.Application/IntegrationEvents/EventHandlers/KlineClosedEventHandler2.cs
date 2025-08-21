using MediatR;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Shared;
using Trading.Domain.IRepositories;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Application.IntegrationEvents.EventHandlers;

public class KlineClosedEventHandler2 : INotificationHandler<KlineClosedEvent>
{
    private readonly IAlertNotificationService _alertNotificationService;
    private readonly IAlertRepository _alertRepository;
    private readonly GlobalState _globalState;

    public KlineClosedEventHandler2(
        IAlertNotificationService alertNotificationService,
        IAlertRepository alertRepository,
        GlobalState globalState)
    {
        _alertNotificationService = alertNotificationService;
        _alertRepository = alertRepository;
        _globalState = globalState;
    }

    public virtual async Task Handle(KlineClosedEvent @event, CancellationToken cancellationToken)
    {
        var alerts = await _alertRepository.GetAllAsync();
        foreach (var alert in alerts)
        {
            if (alert.Symbol == @event.Symbol && alert.Interval == BinanceHelper.ConvertToIntervalString(@event.Interval))
            {
                var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";

                if (alert.Status == Common.Enums.Status.Paused)
                {
                    alert.Resume();
                    await _alertRepository.UpdateAsync(alert.Id, alert, cancellationToken);
                }
                else if (alert.Status == Common.Enums.Status.Running)
                {
                    await _alertNotificationService.SendNotification(alert, @event.Kline, cancellationToken);
                }

                // 最后global state中的kline, 确保alert不会短时间内连续触发2次
                _globalState.AddOrUpdateLastKline(key, @event.Kline);
            }
        }
    }
}
