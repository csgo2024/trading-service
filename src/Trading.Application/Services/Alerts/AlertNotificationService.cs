using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Services.Shared;
using Trading.Application.Telegram.Logging;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Services.Alerts;

public interface IAlertNotificationService
{
    Task SendNotification(Alert alert, CancellationToken cancellationToken);
}

public class AlertNotificationService : IAlertNotificationService
{
    private readonly ILogger<AlertNotificationService> _logger;
    private readonly IAlertRepository _alertRepository;
    private readonly JavaScriptEvaluator _javaScriptEvaluator;
    private readonly GlobalState _globalState;

    public AlertNotificationService(
        ILogger<AlertNotificationService> logger,
        IAlertRepository alertRepository,
        JavaScriptEvaluator javaScriptEvaluator,
        GlobalState globalState)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _javaScriptEvaluator = javaScriptEvaluator;
        _globalState = globalState;
    }
    public async Task SendNotification(Alert alert, CancellationToken cancellationToken)
    {
        var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_globalState.TryGetLastKline(key, out var kline) && kline != null)
                {
                    var met = _javaScriptEvaluator.EvaluateExpression(alert.Expression,
                                                                      kline.OpenPrice,
                                                                      kline.ClosePrice,
                                                                      kline.HighPrice,
                                                                      kline.LowPrice);
                    if ((DateTime.UtcNow - alert.LastNotification).TotalSeconds >= 60 && met)
                    {
                        await SendNotification(alert, kline);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alert {AlertId}", alert.Id);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private async Task SendNotification(Alert alert, IBinanceKline kline)
    {
        try
        {

            var priceChange = kline.ClosePrice - kline.OpenPrice;
            var priceChangePercent = priceChange / kline.OpenPrice * 100;
            var changeText = priceChange >= 0 ? "🟢 " : "🔴 ";

            var text = $"""
            Expression: {alert.Expression}
            Open: {kline.OpenPrice} Close: {kline.ClosePrice}
            High: {kline.HighPrice} Low: {kline.LowPrice}
            {changeText}: {priceChange:F3} ({priceChangePercent:F3}%)
            """;

            var telegramScope = new TelegramLoggerScope
            {
                Title = $"⏰ Alarm Triggered: {alert.Symbol}-{alert.Interval}",
                DisableNotification = false,
                ReplyMarkup = new InlineKeyboardMarkup(
                [
                    [
                        InlineKeyboardButton.WithCallbackData("Pause", $"alert_pause_{alert.Id}")
                    ]
                ])
            };
            using (_logger.BeginScope(telegramScope))
            {
                _logger.LogInformation(text);
            }

            alert.LastNotification = DateTime.UtcNow;
            alert.UpdatedAt = DateTime.UtcNow;
            _globalState.AddOrUpdateAlert(alert.Id, alert);
            await _alertRepository.UpdateAsync(alert.Id, alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }
}
