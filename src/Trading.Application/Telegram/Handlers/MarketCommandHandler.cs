using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Logging;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Telegram.Handlers;

public class MarketCommandHandler : ICommandHandler
{
    private readonly ILogger<MarketCommandHandler> _logger;
    private readonly IAccountProcessorFactory _accountProcessorFactory;
    public static string Command => "/market";
    private readonly JsonSerializerOptions _options;
    public MarketCommandHandler(ILogger<MarketCommandHandler> logger,
        IAccountProcessorFactory accountProcessorFactory)
    {
        _logger = logger;
        _accountProcessorFactory = accountProcessorFactory;
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task HandleAsync(string parameters)
    {
        var accountProcessor = _accountProcessorFactory.GetAccountProcessor(AccountType.Future)!;
        var kLines = await accountProcessor.GetKlines("BTCUSDT", KlineInterval.OneDay, limit: 1);

        if (!kLines.Success || kLines.Data.Length == 0)
        {
            _logger.LogErrorNotification("Failed to fetch market data for BTCUSDT. Error: {@Error}", kLines.Error);
            return;
        }
        var kline = kLines.Data[0];
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
        var message = JsonSerializer.Serialize(market, _options);
        _logger.LogInformation(Logging.LoggerExtensions.NotificationEventId, "{message}", message);
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
