using System.ComponentModel.DataAnnotations;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
using Trading.Common.Helpers;

namespace Trading.Exchange.Binance.Helpers;

public static class BinanceHelper
{
    private static readonly Dictionary<string, KlineInterval> _stringToInterval = new(StringComparer.OrdinalIgnoreCase)
    {
        { "5m", KlineInterval.FiveMinutes },
        { "15m", KlineInterval.FifteenMinutes },
        { "1h", KlineInterval.OneHour },
        { "4h", KlineInterval.FourHour },
        { "1d", KlineInterval.OneDay },
        { "3d", KlineInterval.ThreeDay },
        { "1w", KlineInterval.OneWeek },
    };

    public static IReadOnlyDictionary<string, KlineInterval> KlineIntervalDict => _stringToInterval;

    private static readonly Dictionary<KlineInterval, string> _intervalToString =
        _stringToInterval.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// 根据下单时间和K线周期，计算出下单时的K线周期的开始和结束时间
    /// </summary>
    /// <param name="utcTime"></param>
    /// <param name="interval"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static (DateTime Open, DateTime Close) GetKLinePeriod(DateTime utcTime, string interval)
    {
        // 数字部分
        var value = int.Parse(new string([.. interval.TakeWhile(char.IsDigit)]));
        // 单位部分
        var unit = new string([.. interval.SkipWhile(char.IsDigit)]);

        TimeSpan period;
        DateTime baseTime;
        switch (unit.ToLower())
        {
            case "m":
                period = TimeSpan.FromMinutes(value);
                baseTime = new DateTime(utcTime.Year, utcTime.Month, utcTime.Day, utcTime.Hour, 0, 0, DateTimeKind.Utc);
                break;
            case "h":
                period = TimeSpan.FromHours(value);
                baseTime = new DateTime(utcTime.Year, utcTime.Month, utcTime.Day, 0, 0, 0, DateTimeKind.Utc);
                break;
            case "d":
                period = TimeSpan.FromDays(value);
                baseTime = new DateTime(utcTime.Year, utcTime.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                break;
            case "w":
                period = TimeSpan.FromDays(value * 7);
                var daysFromMonday = ((int)utcTime.DayOfWeek + 6) % 7;
                baseTime = utcTime.Date.AddDays(-daysFromMonday);
                baseTime = DateTime.SpecifyKind(baseTime, DateTimeKind.Utc);
                break;
            default:
                throw new ArgumentException("Unsupported interval: {interval}");
        }
        var block = (utcTime - baseTime).Ticks / period.Ticks;
        var start = baseTime.AddTicks(block * period.Ticks);
        return (start, start + period);
    }

    public static KlineInterval ConvertToKlineInterval(string interval) =>
        _stringToInterval.TryGetValue(interval, out var result)
            ? result
            : throw new ValidationException($"Invalid interval: '{interval}'");

    public static string ConvertToIntervalString(KlineInterval interval) =>
        _intervalToString.TryGetValue(interval, out var result)
            ? result
            : throw new ValidationException($"Invalid KlineInterval: '{interval}'");

    public static decimal AdjustPriceByStepSize(decimal price, BinanceSymbolPriceFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var adjustedPrice = Math.Round(price / filter.TickSize, MidpointRounding.ToZero) * filter.TickSize;
        if (adjustedPrice < filter.MinPrice)
        {
            throw new InvalidOperationException($"Price must be greater than {filter.MinPrice}");
        }
        if (adjustedPrice > filter.MaxPrice)
        {
            throw new InvalidOperationException($"Price must be less than {filter.MaxPrice}");
        }
        return CommonHelper.TrimEndZero(adjustedPrice);
    }

    public static decimal AdjustQuantityBystepSize(decimal quantity, BinanceSymbolLotSizeFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var adjustedQuantity = Math.Round(quantity / filter.StepSize, MidpointRounding.ToZero) * filter.StepSize;
        if (adjustedQuantity < filter.MinQuantity)
        {
            throw new InvalidOperationException($"Quantity must be greater than {filter.MinQuantity}");
        }
        if (adjustedQuantity > filter.MaxQuantity)
        {
            throw new InvalidOperationException($"Quantity must be less than {filter.MaxQuantity}");
        }
        return CommonHelper.TrimEndZero(adjustedQuantity);
    }
}
