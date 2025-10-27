namespace Trading.Exchange.Abstraction.Objects;

public sealed record KlineInterval(string Code)
{
    public static readonly KlineInterval OneSecond = new("1s");
    public static readonly KlineInterval OneMinute = new("1m");
    public static readonly KlineInterval ThreeMinutes = new("3m");
    public static readonly KlineInterval FiveMinutes = new("5m");
    public static readonly KlineInterval FifteenMinutes = new("15m");
    public static readonly KlineInterval ThirtyMinutes = new("30m");
    public static readonly KlineInterval OneHour = new("1h");
    public static readonly KlineInterval TwoHour = new("2h");
    public static readonly KlineInterval FourHour = new("4h");
    public static readonly KlineInterval SixHour = new("6h");
    public static readonly KlineInterval EightHour = new("8h");
    public static readonly KlineInterval TwelveHour = new("12h");
    public static readonly KlineInterval OneDay = new("1d");
    public static readonly KlineInterval ThreeDay = new("3d");
    public static readonly KlineInterval OneWeek = new("1w");
    public static readonly KlineInterval OneMonth = new("1M");

    public static readonly IReadOnlyList<KlineInterval> All =
    [
        OneSecond,
        OneMinute,
        ThreeMinutes,
        FiveMinutes,
        FifteenMinutes,
        ThirtyMinutes,
        OneHour,
        TwoHour,
        FourHour,
        SixHour,
        EightHour,
        TwelveHour,
        OneDay,
        ThreeDay,
        OneWeek,
        OneMonth
    ];
    public static KlineInterval? FromCode(string code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

    public static implicit operator KlineInterval(string code)
    {
        var found = FromCode(code) ?? throw new ArgumentException($"Invalid interval: {code}", nameof(code));
        return found;
    }

    public static implicit operator string(KlineInterval interval) => interval.Code;

    public override string ToString() => Code;
}
