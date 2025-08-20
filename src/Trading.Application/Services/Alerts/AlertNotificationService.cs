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
    Task ProcessAlertAsync(Alert alert, CancellationToken cancellationToken);
    Task SendNotification(Alert alert, IBinanceKline kline, CancellationToken cancellationToken);
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
    public async Task ProcessAlertAsync(Alert alert, CancellationToken cancellationToken)
    {
        var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // 确保获取到最新的 alert 状态
                _globalState.TryGetAlert(alert.Id, out alert!);

                // 计算距离上次通知经过的时间
                var elapsed = (DateTime.UtcNow - alert.LastNotification).TotalSeconds;
                if (elapsed >= 60)
                {
                    if (_globalState.TryGetLastKline(key, out var kline) && kline != null)
                    {
                        await SendNotification(alert, kline, cancellationToken);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking alert {AlertId}", alert.Id);
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }
    }

    public async Task SendNotification(Alert alert, IBinanceKline kline, CancellationToken cancellationToken)
    {
        try
        {
            var met = _javaScriptEvaluator.EvaluateExpression(
                alert.Expression,
                kline.OpenPrice,
                kline.ClosePrice,
                kline.HighPrice,
                kline.LowPrice);

            if (!met)
            {
                return; // 条件未满足，不发送通知
            }

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
            await _alertRepository.UpdateAsync(alert.Id, alert, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send alert");
        }
    }
}
