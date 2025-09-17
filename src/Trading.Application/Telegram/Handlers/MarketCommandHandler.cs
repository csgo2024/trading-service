using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScottPlot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Logging;
using Trading.Common.Models;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Telegram.Handlers;

public class MarketCommandHandler : ICommandHandler
{
    private readonly ILogger<MarketCommandHandler> _logger;
    private readonly IAccountProcessorFactory _accountProcessorFactory;
    public static string Command => "/market";
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;

    public MarketCommandHandler(ILogger<MarketCommandHandler> logger,
        IAccountProcessorFactory accountProcessorFactory,
        ITelegramBotClient botClient,
        IOptions<TelegramSettings> settings)
    {
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings), "TelegramSettings is not valid.");
    }

    public async Task HandleAsync(string parameters)
    {
        var (symbol, interval) = ParseParameters(parameters);

        var during = 15;  // 取最近15天的数据

        // Parse the timeframe parameter and set configuration
        var timeframeConfig = interval switch
        {
            "4h" => (
                Interval: KlineInterval.FourHour,
                Limit: 6 * during,
                TimeSpan: TimeSpan.FromHours(4)
            ),
            _ => (
                Interval: KlineInterval.OneDay,
                Limit: during,
                TimeSpan: TimeSpan.FromDays(1)
            )
        };

        var kLines = await GetKlinesAsync(symbol, timeframeConfig.Interval, timeframeConfig.Limit);
        if (kLines.Length == 0)
        {
            return;
        }

        var caption = timeframeConfig.Interval == KlineInterval.OneDay
            ? GetCurrentDayKlineInfo(kLines)
            : GetCurrentDayKlineInfo(await GetKlinesAsync(symbol, KlineInterval.OneDay, 1));

        var pngBytes = GenerateCandleChartImageBytes(kLines, timeframeConfig.TimeSpan);
        using var ms = new MemoryStream(pngBytes);
        ms.Position = 0;
        var inputFile = InputFile.FromStream(ms, fileName: "candlestick.png");

        await _botClient.SendPhoto(chatId: _chatId, photo: inputFile, caption: caption);
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }

    #region Private Methods

    private static (string Symbol, string Interval) ParseParameters(string parameters, string defaultSymbol = "BTCUSDT", string defaultInterval = "1d")
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            return (defaultSymbol, defaultInterval);
        }

        var parameterParts = parameters.Trim().Split([' '], 2);
        var symbol = parameterParts[0].ToUpper();
        var interval = parameterParts.Length > 1 ? parameterParts[1] : defaultInterval;

        return (symbol, interval);
    }

    private static byte[] GenerateCandleChartImageBytes(IBinanceKline[] klines, TimeSpan span)
    {
        using var plt = new Plot();
        var ohlcs = klines.Select(k => new OHLC(
            open: decimal.ToDouble(k.OpenPrice),
            high: decimal.ToDouble(k.HighPrice),
            low: decimal.ToDouble(k.LowPrice),
            close: decimal.ToDouble(k.ClosePrice),
            start: k.OpenTime,
            span: span)).ToList();
        var candles = plt.Add.Candlestick(ohlcs);
        candles.Axes.YAxis = plt.Axes.Right;

        plt.Axes.Right.TickLabelStyle.FontName = "DejaVu Sans";
        plt.Axes.Right.TickLabelStyle.FontSize = 12;

        plt.Axes.DateTimeTicksBottom();
        return plt.GetImageBytes(600, 600, ImageFormat.Png);
    }

    private async Task<IBinanceKline[]> GetKlinesAsync(string symbol, KlineInterval interval, int limit, DateTime? startTime = null, DateTime? endTime = null)
    {
        var accountProcessor = _accountProcessorFactory.GetAccountProcessor(AccountType.Future)!;
        var result = await accountProcessor.GetKlines(symbol, interval, startTime, endTime, limit);
        if (!result.Success || result.Data.Length == 0)
        {
            _logger.LogErrorNotification("Failed to fetch market data for {Symbol}. Error: {@Error}", symbol, result.Error);
            return [];
        }
        return result.Data;
    }

    private static string GetCurrentDayKlineInfo(IBinanceKline[]? dailyKlines = null)
    {
        IBinanceKline kline;

        if (dailyKlines != null && dailyKlines.Length > 0)
        {
            kline = dailyKlines.Last();
        }
        else
        {
            return string.Empty;
        }

        var priceChange = kline.ClosePrice - kline.OpenPrice;
        var priceChangePercent = priceChange / kline.OpenPrice * 100;
        var changeText = priceChange >= 0 ? "上涨" : "下跌";

        var result = $"""
Date={DateTime.UtcNow.AddHours(8):yyyy-MM-dd}
Info={changeText}: {priceChange:F3} ({priceChangePercent:F2}%)
Close={kline.ClosePrice}
Open={kline.OpenPrice}
Low={kline.LowPrice}
High={kline.HighPrice}
""";
        return result;
    }
    #endregion
}
