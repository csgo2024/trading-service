using Microsoft.Extensions.Logging;

namespace Trading.Application.Telegram.Logging;

public static class LoggerExtensions
{

    public static readonly EventId NotificationEventId = new(9999, "Notification");

    public static void LogErrorNotification(this ILogger logger, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Error, null, false, null, message, args);
    }
    public static void LogErrorNotification(this ILogger logger, Exception? ex, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Error, null, false, ex, message, args);
    }

    public static void LogInfoNotification(this ILogger logger, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Information, null, true, null, message, args);
    }
    public static void LogInfoNotification(this ILogger logger, bool disableNotification, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Information, null, disableNotification, null, message, args);
    }
    public static void LogInfoNotification(this ILogger logger, string? title, bool disableNotification, string? message, params object?[] args)
    {
        LogNotification(logger, LogLevel.Information, title, disableNotification, null, message, args);
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
