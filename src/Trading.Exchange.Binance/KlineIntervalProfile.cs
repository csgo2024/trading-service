using AutoMapper;
using KlineInterval = Trading.Exchange.Abstraction.Objects.KlineInterval;
using KlineIntervalEnum = Binance.Net.Enums.KlineInterval;

public class KlineIntervalProfile : Profile
{
    public KlineIntervalProfile()
    {
        // Record -> Enum
        CreateMap<KlineInterval, KlineIntervalEnum>()
            .ConvertUsing(src => MapToEnum(src));

        // Enum -> Record
        CreateMap<KlineIntervalEnum, KlineInterval>()
            .ConvertUsing(src => MapToRecord(src));
    }

    private static KlineIntervalEnum MapToEnum(KlineInterval src)
    {
        if (src == KlineInterval.OneSecond)
        {
            return KlineIntervalEnum.OneSecond;
        }

        if (src == KlineInterval.OneMinute)
        {
            return KlineIntervalEnum.OneMinute;
        }

        if (src == KlineInterval.ThreeMinutes)
        {
            return KlineIntervalEnum.ThreeMinutes;
        }

        if (src == KlineInterval.FiveMinutes)
        {
            return KlineIntervalEnum.FiveMinutes;
        }

        if (src == KlineInterval.FifteenMinutes)
        {
            return KlineIntervalEnum.FifteenMinutes;
        }

        if (src == KlineInterval.ThirtyMinutes)
        {
            return KlineIntervalEnum.ThirtyMinutes;
        }

        if (src == KlineInterval.OneHour)
        {
            return KlineIntervalEnum.OneHour;
        }

        if (src == KlineInterval.TwoHour)
        {
            return KlineIntervalEnum.TwoHour;
        }

        if (src == KlineInterval.FourHour)
        {
            return KlineIntervalEnum.FourHour;
        }

        if (src == KlineInterval.SixHour)
        {
            return KlineIntervalEnum.SixHour;
        }

        if (src == KlineInterval.EightHour)
        {
            return KlineIntervalEnum.EightHour;
        }

        if (src == KlineInterval.TwelveHour)
        {
            return KlineIntervalEnum.TwelveHour;
        }

        if (src == KlineInterval.OneDay)
        {
            return KlineIntervalEnum.OneDay;
        }

        if (src == KlineInterval.ThreeDay)
        {
            return KlineIntervalEnum.ThreeDay;
        }

        if (src == KlineInterval.OneWeek)
        {
            return KlineIntervalEnum.OneWeek;
        }

        if (src == KlineInterval.OneMonth)
        {
            return KlineIntervalEnum.OneMonth;
        }

        throw new ArgumentException($"无效的映射: {src}");
    }

    private static KlineInterval MapToRecord(KlineIntervalEnum src)
    {
        if (src == KlineIntervalEnum.OneSecond)
        {
            return KlineInterval.OneSecond;
        }

        if (src == KlineIntervalEnum.OneMinute)
        {
            return KlineInterval.OneMinute;
        }

        if (src == KlineIntervalEnum.ThreeMinutes)
        {
            return KlineInterval.ThreeMinutes;
        }

        if (src == KlineIntervalEnum.FiveMinutes)
        {
            return KlineInterval.FiveMinutes;
        }

        if (src == KlineIntervalEnum.FifteenMinutes)
        {
            return KlineInterval.FifteenMinutes;
        }

        if (src == KlineIntervalEnum.ThirtyMinutes)
        {
            return KlineInterval.ThirtyMinutes;
        }

        if (src == KlineIntervalEnum.OneHour)
        {
            return KlineInterval.OneHour;
        }

        if (src == KlineIntervalEnum.TwoHour)
        {
            return KlineInterval.TwoHour;
        }

        if (src == KlineIntervalEnum.FourHour)
        {
            return KlineInterval.FourHour;
        }

        if (src == KlineIntervalEnum.SixHour)
        {
            return KlineInterval.SixHour;
        }

        if (src == KlineIntervalEnum.EightHour)
        {
            return KlineInterval.EightHour;
        }

        if (src == KlineIntervalEnum.TwelveHour)
        {
            return KlineInterval.TwelveHour;
        }

        if (src == KlineIntervalEnum.OneDay)
        {
            return KlineInterval.OneDay;
        }

        if (src == KlineIntervalEnum.ThreeDay)
        {
            return KlineInterval.ThreeDay;
        }

        if (src == KlineIntervalEnum.OneWeek)
        {
            return KlineInterval.OneWeek;
        }

        if (src == KlineIntervalEnum.OneMonth)
        {
            return KlineInterval.OneMonth;
        }

        throw new ArgumentException($"无效的映射: {src}");
    }
}
