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

internal sealed class DisposableScope : IDisposable
{
    private readonly Action _onDispose;

    public DisposableScope(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        _onDispose();
    }
}

public class TelegramLoggerScope
{
    public string? Title { get; set; }
    public bool? DisableNotification { get; set; }
    public ParseMode? ParseMode { get; set; }
    public ReplyMarkup? ReplyMarkup { get; set; }
}

public class TelegramLogger : ILogger
{
    private readonly IOptions<TelegramLoggerOptions> _loggerOptions;
    private readonly ITelegramBotClient _botClient;
    private readonly string _categoryName;
    private readonly string _chatId;
    private readonly AsyncLocal<Stack<TelegramLoggerScope>> _scopeStack = new();

    public TelegramLogger(ITelegramBotClient botClient,
                          IOptions<TelegramLoggerOptions> loggerOptions,
                          TelegramSettings settings,
                          string categoryName)
    {
        _botClient = botClient;
        _loggerOptions = loggerOptions;
        _categoryName = categoryName;
        _chatId = settings.ChatId ?? throw new ArgumentNullException(nameof(settings), "TelegramSettings is not valid.");
    }

    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        var scopeStack = _scopeStack.Value ??= new Stack<TelegramLoggerScope>();

        if (state is not TelegramLoggerScope scope)
        {
            scope = new TelegramLoggerScope();
        }

        scopeStack.Push(scope);

        return new DisposableScope(() =>
        {
            if (_scopeStack.Value?.Count > 0)
            {
                _scopeStack.Value.Pop();
            }
        });
    }

    /// <summary>
    /// 获取合并后的作用域，内层优先，逐级向外继承
    /// </summary>
    private TelegramLoggerScope GetCurrentScope()
    {
        var scopeStack = _scopeStack.Value;
        if (scopeStack == null || scopeStack.Count == 0)
        {
            var scope = new TelegramLoggerScope
            {
                DisableNotification = true,
                ParseMode = ParseMode.Html
            };
            return scope;
        }

        var merged = new TelegramLoggerScope();

        // ⚠️ Stack 默认迭代顺序：先栈顶（内层），再往外层
        foreach (var scope in scopeStack)
        {
            if (merged.Title == null && !string.IsNullOrEmpty(scope.Title))
            {
                merged.Title = scope.Title;
            }

            if (merged.DisableNotification == null && scope.DisableNotification != null)
            {
                merged.DisableNotification = scope.DisableNotification;
            }

            if (merged.ParseMode == null && scope.ParseMode != null)
            {
                merged.ParseMode = scope.ParseMode;
            }

            if (merged.ReplyMarkup == null && scope.ReplyMarkup != null)
            {
                merged.ReplyMarkup = scope.ReplyMarkup;
            }
        }

        // 默认值补齐
        merged.DisableNotification ??= true;
        merged.ParseMode ??= ParseMode.Html;

        return merged;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None
            && logLevel >= _loggerOptions.Value.MinimumLevel
            && !_loggerOptions.Value.ExcludeCategories.Contains(_categoryName);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }
        if (eventId != LoggerExtensions.NotificationEventId)
        {
            // Only process NotificationEventId
            return;
        }
        // Fire-and-forget
        _ = Task.Run(() => LogInternalAsync(logLevel, state, exception, formatter));
    }

    private async Task LogInternalAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
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
                ParseMode = scope.ParseMode ?? ParseMode.Html,
                DisableNotification = scope.DisableNotification ?? true,
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

    private string BuildHtmlMessage<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter, TelegramLoggerScope scope)
    {
        var message = new StringBuilder();

        var title = !string.IsNullOrEmpty(scope.Title)
            ? scope.Title
            : $"{GetEmoji(logLevel)} {logLevel}";

        if (title.Length > 15)
        {
            message.AppendLine($"<b>{title}</b>");
            message.AppendLine($"<b>⏰ ({DateTime.UtcNow.AddHours(8):yyyy-MM-dd HH:mm:ss}) </b>");
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
