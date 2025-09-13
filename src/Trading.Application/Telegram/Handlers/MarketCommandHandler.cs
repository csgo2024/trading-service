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

    public static byte[] DrawCandleChartToBytes(List<IBinanceKline> klines)
    {
        var plt = new Plot();
        var ohlcs = klines.Select(k => new OHLC(
            open: decimal.ToDouble(k.OpenPrice),
            high: decimal.ToDouble(k.HighPrice),
            low: decimal.ToDouble(k.LowPrice),
            close: decimal.ToDouble(k.ClosePrice),
            start: k.OpenTime,
            span: TimeSpan.FromDays(1))).ToList();
        plt.Add.Candlestick(ohlcs);
        plt.Axes.Bottom.TickLabelStyle.Rotation = -45;

        plt.Axes.DateTimeTicksBottom();

        var bytes = plt.GetImageBytes(600, 600, ImageFormat.Png);
        return bytes;
    }
    public async Task HandleAsync(string parameters)
    {
        var now = DateTime.UtcNow;
        var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
        var weekStart = now.Date.AddDays(-1 * diff);

        var accountProcessor = _accountProcessorFactory.GetAccountProcessor(AccountType.Future)!;
        var kLines = await accountProcessor.GetKlines("BTCUSDT", KlineInterval.OneDay, startTime: weekStart, endTime: now, limit: 7);

        if (!kLines.Success || kLines.Data.Length == 0)
        {
            _logger.LogErrorNotification("Failed to fetch market data for BTCUSDT. Error: {@Error}", kLines.Error);
            return;
        }
        var kline = kLines.Data.Last();
        var priceChange = kline.ClosePrice - kline.OpenPrice;
        var priceChangePercent = priceChange / kline.OpenPrice * 100;
        var changeText = priceChange >= 0 ? "上涨" : "下跌";
        var info = $"{changeText}: {priceChange:F3} ({priceChangePercent:F3}%)";
        var market = new
        {
            kline.OpenPrice,
            kline.ClosePrice,
            kline.LowPrice,
            kline.HighPrice,
            Info = info,
        };

        var pngBytes = DrawCandleChartToBytes(kLines.Data.ToList());
        using var ms = new MemoryStream(pngBytes);
        ms.Position = 0;

        var inputFile = InputFile.FromStream(ms, fileName: "candlestick.png");

        var caption = JsonSerializer.Serialize(market, _options);
        await _botClient.SendPhoto(
            chatId: _chatId,
            photo: inputFile,
            caption: caption
        );
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
