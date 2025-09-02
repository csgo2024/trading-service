using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Logging;

namespace Trading.Application.Telegram.Handlers;

public class HelpCommandHandler : ICommandHandler
{
    private readonly ILogger<HelpCommandHandler> _logger;
    public static string Command => "/help";

    public HelpCommandHandler(ILogger<HelpCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(string parameters)
    {
        var telegramScope = new TelegramLoggerScope
        {
            ParseMode = ParseMode.None,
        };

        using (_logger.BeginScope(telegramScope))
        {
            _logger.LogInformation(Logging.LoggerExtensions.NotificationEventId, "https://csgo2024.github.io/trading-service/");
        }
        return Task.CompletedTask;
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
