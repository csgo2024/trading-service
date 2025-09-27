using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Trading.Application.Telegram;
using Trading.Application.Telegram.Logging;
using Trading.Common.Models;
using Trading.Common.Services;

namespace Trading.API.HostServices;

public class TelegramBotService : BackgroundService
{
    private readonly ILogger<TelegramBotService> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramCommandHandler _commandHandler;
    private readonly TelegramSettings _telegramSettings;
    private readonly IStringLocalizer<TelegramBotService> _localizer;
    private readonly ILanguageService _languageService;

    public TelegramBotService(
        ITelegramBotClient botClient,
        ITelegramCommandHandler commandHandler,
        IOptions<TelegramSettings> telegramSettingOptions,
        ILogger<TelegramBotService> logger,
        IStringLocalizer<TelegramBotService> localizer,
        ILanguageService languageService)
    {
        _botClient = botClient;
        _commandHandler = commandHandler;
        _logger = logger;
        _telegramSettings = telegramSettingOptions.Value;
        _localizer = localizer;
        _languageService = languageService;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: new ReceiverOptions(),
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogErrorNotification(ex, _localizer["FailedToStartBotService"]);
        }

        await Task.Delay(-1, cancellationToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.CallbackQuery is { } callbackQuery)
            {
                var languageCode = callbackQuery.From?.LanguageCode ?? "en";
                _languageService.SetCurrentCulture(languageCode);
                if (callbackQuery.From?.Id != _telegramSettings.UserId)
                {
                    _logger.LogErrorNotification(_localizer["PermissionDenied"]);
                    return;
                }
                await _commandHandler.HandleCallbackQuery(callbackQuery);
            }

            if (update.Message is { } message && message.Text is { } messageText)
            {
                var languageCode = message.From?.LanguageCode ?? "en";
                _languageService.SetCurrentCulture(languageCode);

                if (update.Message.From?.Id != _telegramSettings.UserId)
                {
                    _logger.LogErrorNotification(_localizer["PermissionDenied"]);
                    return;
                }
                if (messageText.StartsWith('/'))
                {
                    await _commandHandler.HandleCommand(message);
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogErrorNotification(ex, "Error handling update {UpdateId}", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        // _logger.LogErrorNotification(exception, "Telegram Polling Error");
        return Task.CompletedTask;
    }
}
