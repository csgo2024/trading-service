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

    /// <summary>
    /// Kline关闭时，检查所有Alert是否满足触发条件
    /// 并检查对应的Alert是否是Paused状态，如果是则Resume
    /// </summary>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
                    // 立即检查Alert是否满足触发条件，不再等待对应的Task去检查，确保在Kline关闭时能第一时间触发
                    await _alertNotificationService.SendNotification(alert, @event.Kline, cancellationToken);
                }

                // 最后global state中的kline, 确保alert不会短时间内连续触发2次
                _globalState.AddOrUpdateLastKline(key, @event.Kline);
            }
        }
    }
}
