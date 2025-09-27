using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScottPlot;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Logging;
using Trading.Common.Models;
using Trading.Exchange.Binance.Extensions;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Telegram.Handlers;

public static class ChartGenerator
{
    private const int Width = 600;
    private const int Height = 600;
    private const string FontName = "DejaVu Sans";

    public static byte[] Candlestick(IBinanceKline[] klines, TimeSpan timeSpan)
    {
        var plot = new Plot();
        plot.Axes.Right.TickLabelStyle.FontName = FontName;
        plot.Axes.Right.TickLabelStyle.FontSize = 12;

        var ohlcs = klines.Select(k => new OHLC(
            (double)k.OpenPrice,
            (double)k.HighPrice,
            (double)k.LowPrice,
            (double)k.ClosePrice,
            k.OpenTime,
            timeSpan)).ToList();

        var candlestickPlot = plot.Add.Candlestick(ohlcs);
        candlestickPlot.Axes.YAxis = plot.Axes.Right;

        plot.Axes.DateTimeTicksBottom();
        return plot.GetImageBytes(Width, Height, ImageFormat.Png);
    }
}

public class MarketCommandHandler : ICommandHandler
{
    private readonly ILogger<MarketCommandHandler> _logger;
    private readonly IAccountProcessorFactory _accountProcessorFactory;
    private readonly IStringLocalizer<MarketCommandHandler> _localizer;
    public static string Command => "/market";
    private readonly ITelegramBotClient _botClient;
    private readonly string _chatId;

    public MarketCommandHandler(ILogger<MarketCommandHandler> logger,
        IAccountProcessorFactory accountProcessorFactory,
        ITelegramBotClient botClient,
        IOptions<TelegramSettings> settings,
        IStringLocalizer<MarketCommandHandler> localizer)
    {
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _localizer = localizer;
        _botClient = botClient;
        _chatId = settings.Value.ChatId ?? throw new ArgumentNullException(nameof(settings), "TelegramSettings is not valid.");
    }

    public async Task HandleAsync(string parameters)
    {
        _logger.LogInfoNotification(_localizer["market.command.recieved"]);
        var (symbol, interval) = ParseParameters(parameters);

        var during = 15;

        // Parse the timeframe parameter and set configuration
        var config = interval switch
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

        var kLines = await GetKlinesAsync(symbol, config.Interval, config.Limit);
        if (kLines.Length == 0)
        {
            return;
        }

        var caption = await GenerateDailySummaryAsync(symbol, kLines);
        var bytes = ChartGenerator.Candlestick(kLines, config.TimeSpan);
        using var ms = new MemoryStream(bytes);
        ms.Position = 0;
        var inputFile = InputFile.FromStream(ms, fileName: "candlestick.png");

        await _botClient.SendPhoto(chatId: _chatId, photo: inputFile, caption: caption, parseMode: ParseMode.Html);
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
        var symbol = parameterParts[0].ToUpper().Trim();
        var interval = parameterParts.Length > 1 ? parameterParts[1].Trim() : defaultInterval;

        return (symbol, interval);
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

    private async Task<string> GenerateDailySummaryAsync(string symbol, IBinanceKline[] klines)
    {
        var kline = klines.LastOrDefault();

        if (kline == null || !kline.IsDailyKline())
        {
            var dailyKlines = await GetKlinesAsync(symbol, KlineInterval.OneDay, 1);
            kline = dailyKlines.LastOrDefault();
        }
        if (kline == null)
        {
            return string.Empty;
        }

        var priceChange = kline.ClosePrice - kline.OpenPrice;
        var priceChangePercent = priceChange / kline.OpenPrice * 100;
        var changeText = priceChange > 0 ? "上涨" : "下跌";

        var result = $$"""
{{symbol}}{{changeText}}: {{priceChange:F3}} ({{priceChangePercent:F2}}%)
close: {{kline.ClosePrice}} open: {{kline.OpenPrice}}
low: {{kline.LowPrice}} high: {{kline.HighPrice}}
""";
        return result;
    }
    #endregion
}
