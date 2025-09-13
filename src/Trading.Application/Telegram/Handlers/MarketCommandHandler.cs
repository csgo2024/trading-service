using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    private readonly JsonSerializerOptions _options;
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
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task HandleAsync(string parameters)
    {
        var now = DateTime.UtcNow;
        var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStart = now.Date.AddDays(-1 * diff);

        // Parse the timeframe parameter and set configuration
        var timeframeConfig = (parameters?.Trim().ToLower() ?? "1d") switch
        {
            "1h" => (
                Interval: KlineInterval.OneHour,
                Limit: 24 * 7,
                TimeSpan: TimeSpan.FromHours(1)
            ),
            "4h" => (
                Interval: KlineInterval.FourHour,
                Limit: 6 * 7,
                TimeSpan: TimeSpan.FromHours(4)
            ),
            _ => (
                Interval: KlineInterval.OneDay,
                Limit: 7,
                TimeSpan: TimeSpan.FromDays(1)
            )
        };

        var kLines = await GetKlinesAsync("BTCUSDT", timeframeConfig.Interval, timeframeConfig.Limit, weekStart, now);
        if (kLines.Length == 0)
        {
            return;
        }
        string caption;
        if (timeframeConfig.Interval == KlineInterval.OneDay)
        {
            caption = GetCurrentDayKlineInfo(kLines);
        }
        else
        {
            var dailyKlines = await GetKlinesAsync("BTCUSDT", KlineInterval.OneDay, 1);
            caption = GetCurrentDayKlineInfo(dailyKlines);
        }

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
        plt.Add.Candlestick(ohlcs);
        plt.Axes.Bottom.TickLabelStyle.Rotation = -45;
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

    private string GetCurrentDayKlineInfo(IBinanceKline[]? dailyKlines = null)
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

        var market = new
        {
            Date = $"{DateTime.UtcNow.AddHours(8):yyyy-MM-dd}",
            Info = $"{changeText}: {priceChange:F3} ({priceChangePercent:F3}%)",
            kline.OpenPrice,
            kline.ClosePrice,
            kline.LowPrice,
            kline.HighPrice,
        };
        return JsonSerializer.Serialize(market, _options);
    }
    #endregion
}
