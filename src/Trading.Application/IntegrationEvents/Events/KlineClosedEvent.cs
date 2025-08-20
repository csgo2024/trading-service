using Binance.Net.Enums;
using Binance.Net.Interfaces;
using MediatR;

namespace Trading.Application.IntegrationEvents.Events;

public class KlineClosedEvent : INotification
{
    public string Symbol { get; }
    public KlineInterval Interval { get; }
    public IBinanceKline Kline { get; }

    public KlineClosedEvent(string symbol, KlineInterval interval, IBinanceKline kline)
    {
        Symbol = symbol;
        Interval = interval;
        Kline = kline;
    }
}
