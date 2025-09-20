using Binance.Net.Interfaces;

namespace Trading.Exchange.Binance.Extensions;

public static class KlineExtensions
{
    public static bool IsDailyKline(this IBinanceKline kline)
    {
        var expectedClose = kline.OpenTime.AddHours(24);
        var diff = Math.Abs((kline.CloseTime - expectedClose).TotalSeconds);
        return diff <= 10;
    }
}
