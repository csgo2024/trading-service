using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Common.Extensions;
using Trading.Common.Models;

namespace Trading.Application.Telegram.Logging;
public class TelegramLoggerScope
{
    public string? Title { get; set; }
    public bool DisableNotification { get; set; } = true;
    public ParseMode ParseMode { get; set; } = ParseMode.Html;
    public ReplyMarkup? ReplyMarkup { get; set; }
}

public class TelegramLogger : ILogger
{
    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;
    private readonly ITelegramBotClient _botClient;
    private readonly string _categoryName;
    private readonly string _chatId;
    private readonly IExternalScopeProvider _scopeProvider;

    public TelegramLogger(
        ITelegramBotClient botClient,
        IOptions<TelegramLoggerOptions> loggerOptions,
        TelegramSettings settings,
        string categoryName,
        IExternalScopeProvider? scopeProvider = null)
    {
        _botClient = botClient;
        _loggerOptions = loggerOptions;
        _categoryName = categoryName;
        _chatId = settings.ChatId ?? throw new ArgumentNullException(nameof(settings), "TelegramSettings is not valid.");

        // 如果外部没传 scopeProvider，就用默认的
        _scopeProvider = scopeProvider ?? new LoggerExternalScopeProvider();
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return _scopeProvider.Push(state);
    }

    /// <summary>
    /// 获取当前合并后的 Scope（内层优先覆盖外层）
    /// </summary>
    private TelegramLoggerScope GetCurrentScope()
    {
        var merged = new TelegramLoggerScope();

        _scopeProvider.ForEachScope((s, _) =>
        {
            if (s is TelegramLoggerScope telegramScope)
            {
                if (!string.IsNullOrEmpty(telegramScope.Title))
                {
                    merged.Title = telegramScope.Title;
                }

                if (telegramScope.ReplyMarkup != null)
                {
                    merged.ReplyMarkup = telegramScope.ReplyMarkup;
                }

                merged.DisableNotification = telegramScope.DisableNotification;
                merged.ParseMode = telegramScope.ParseMode;
            }
        }, state: (object?)null);

        return merged;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None
            && logLevel >= _loggerOptions.Value.MinimumLevel
            && !_loggerOptions.Value.ExcludeCategories.Contains(_categoryName);
    }

    public void Log<TState>(LogLevel logLevel,
                            EventId eventId,
                            TState state,
                            Exception? exception,
                            Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }
        if (eventId != LoggerExtensions.NotificationEventId)
        {
            // 只处理 NotificationEventId
            return;
        }

        var task = LogInternalAsync(logLevel, state, exception, formatter);
        task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private async Task LogInternalAsync<TState>(
        LogLevel logLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        try
        {
            var scope = GetCurrentScope();

            var text = scope.ParseMode == ParseMode.Html
                ? BuildHtmlMessage(logLevel, state, exception, formatter, scope)
                : formatter(state, exception);

            await _botClient.SendRequest(new SendMessageRequest
            {
                ChatId = _chatId,
                Text = text,
                ParseMode = scope.ParseMode,
                DisableNotification = scope.DisableNotification,
                ReplyMarkup = scope.ReplyMarkup
            });
        }
        catch (Exception ex)
        {
            try
            {
                await _botClient.SendRequest(new SendMessageRequest
                {
                    ChatId = _chatId,
                    Text = $"Failed to send log message: {ex.Message}",
                    ParseMode = ParseMode.Html,
                });
            }
            catch
            {
                // fallback
            }
        }
    }

    private string BuildHtmlMessage<TState>(
        LogLevel logLevel,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter,
        TelegramLoggerScope scope)
    {
        var message = new StringBuilder();

        var title = !string.IsNullOrEmpty(scope.Title)
            ? scope.Title
            : $"{GetEmoji(logLevel)} {logLevel}";

        if (title.Length > 19)
        {
            message.AppendLine($"<b>{title}</b>");
        }
        else
        {
            message.AppendLine($"<b>{title}</b> ({DateTime.UtcNow.AddHours(8):MM-dd HH:mm:ss})");
        }

        if (_loggerOptions.Value.IncludeCategory)
        {
            message.AppendLine($"📁 {_categoryName}");
        }

        if (exception != null)
        {
            message.AppendLine("<pre>");
            message.AppendLine(exception.Message.ToTelegramSafeString());
            message.AppendLine(formatter(state, exception).ToTelegramSafeString());
            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                message.AppendLine(exception.StackTrace.ToTelegramSafeString());
            }
            message.AppendLine("</pre>");
        }
        else
        {
            message.AppendLine($"<pre>{formatter(state, exception).ToTelegramSafeString()}</pre>");
        }

        return message.ToString();
    }

    public static string GetEmoji(LogLevel level) => level switch
    {
        LogLevel.Trace => "🔍",
        LogLevel.Debug => "🔧",
        LogLevel.Information => "ℹ️",
        LogLevel.Warning => "⚠️",
        LogLevel.Error => "❌",
        LogLevel.Critical => "🆘",
        _ => "📝"
    };
}
