using Microsoft.Extensions.Logging;

namespace Trading.Application.Telegram.Logging;

public static class LoggerExtensions
{

    public static readonly EventId NotificationEventId = new(9999, "Notification");

    /// <summary>
    /// 记录错误通知日志，并发送 Telegram 通知, 通知有提示声音
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void LogErrorNotification(this ILogger logger, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Error, null, false, null, message, args);
    }
    public static void LogErrorNotification(this ILogger logger, Exception? ex, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Error, null, false, ex, message, args);
    }

    /// <summary>
    /// 记录错误通知日志，并发送 Telegram 通知, 通知没有提示声音
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="message"></param>
    /// <param name="args"></param>
    public static void LogInfoNotification(this ILogger logger, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Information, null, true, null, message, args);
    }

    public static void LogNotification(this ILogger logger, LogLevel level, string? title, bool disableNotification, Exception? ex, string? message, params object?[] args)
    {
        var telegramScope = new TelegramLoggerScope
        {
            Title = title,
            DisableNotification = disableNotification
        };

        using (logger.BeginScope(telegramScope))
        {
#pragma warning disable CA2254
            logger.Log(level, NotificationEventId, ex, message, args);
#pragma warning restore CA2254
        }
    }
}
