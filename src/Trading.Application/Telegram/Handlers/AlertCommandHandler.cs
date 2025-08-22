using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Telegram.Bot.Types.ReplyMarkups;
using Trading.Application.Commands;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;

namespace Trading.Application.Telegram.Handlers;

public class AlertCommandHandler : ICommandHandler
{
    private readonly IAlertRepository _alertRepository;
    private readonly ILogger<AlertCommandHandler> _logger;
    private readonly IMediator _mediator;
    public static string Command => "/alert";
    public static string CallbackPrefix => "alert";

    public AlertCommandHandler(ILogger<AlertCommandHandler> logger,
                               IMediator mediator,
                               IAlertRepository alertRepository)
    {
        _logger = logger;
        _alertRepository = alertRepository;
        _mediator = mediator;
    }

    public async Task HandleAsync(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
        {
            await HandleDefault();
            return;
        }

        if (parameters.Trim().Equals("empty", StringComparison.OrdinalIgnoreCase))
        {
            await HandleEmpty();
            return;
        }

        var parts = parameters.Trim().Split([' '], 2);
        var subCommand = parts[0].ToLower();
        var subParameters = parts.Length > 1 ? parts[1] : string.Empty;

        switch (subCommand)
        {
            case "create":
                await HandleCreate(subParameters);
                break;
            case "delete":
                await HandleDelete(subParameters);
                break;
            case "pause":
                await HandlePause(subParameters);
                break;
            case "resume":
                await HandleResume(subParameters);
                break;
            default:
                _logger.LogError("Unknown command. Use: create, delete, pause, or resume");
                break;
        }
    }

    private async Task HandleDefault()
    {
        var alerts = await _alertRepository.GetAllAsync();
        if (alerts.Count == 0)
        {
            _logger.LogInformation("Alert is empty, please create and call later.");
            return;
        }
        foreach (var alert in alerts)
        {
            var (emoji, status) = alert.Status.GetStatusInfo();
            var text = $"""
            {emoji} [{alert.Symbol}-{alert.Interval}]:{status}
            Expression: {alert.Expression}
            """;
            var buttons = alert.Status switch
            {
                Status.Running => [InlineKeyboardButton.WithCallbackData("⏸️ Pause", $"alert_pause_{alert.Id}")],
                Status.Paused => new[] { InlineKeyboardButton.WithCallbackData("▶️ Resume", $"alert_resume_{alert.Id}") },
                _ => throw new InvalidOperationException()
            };
            buttons = [.. buttons, InlineKeyboardButton.WithCallbackData("🗑️ Delete", $"alert_delete_{alert.Id}")];

            var telegramScope = new TelegramLoggerScope
            {
                Title = "⏰ Alarm Status",
                ReplyMarkup = new InlineKeyboardMarkup([buttons])
            };

            using (_logger.BeginScope(telegramScope))
            {
                _logger.LogInformation(text);
            }
        }
    }
    private async Task HandleEmpty()
    {
        var count = await _alertRepository.ClearAllAsync(CancellationToken.None);
        await _mediator.Publish(new AlertEmptyedEvent());
        _logger.LogInformation("{Count} Alarms empty successfully.", count);
    }
    private async Task HandleCreate(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json, nameof(json));
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var command = JsonSerializer.Deserialize<CreateAlertCommand>(json, options)
                      ?? throw new InvalidOperationException("Failed to parse alert parameters");

        await _mediator.Send(command);
    }

    private async Task HandleDelete(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var command = new DeleteAlertCommand { Id = id.Trim() };
        var result = await _mediator.Send(command);
        if (!result)
        {
            throw new InvalidOperationException($"Failed to delete alert {id}");
        }
        _logger.LogInformation("Alert {id} deleted successfully.", id);
    }

    private async Task HandlePause(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            _logger.LogError("Not found alarm: {AlertId}", id);
            return;
        }
        alert.Pause();
        await _alertRepository.UpdateAsync(id, alert);
        _logger.LogInformation("Alert {id} paused successfully.", id);
    }

    private async Task HandleResume(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        var alert = await _alertRepository.GetByIdAsync(id);
        if (alert == null)
        {
            _logger.LogError("Not found alarm: {AlertId}", id);
            return;
        }
        alert.Resume();
        await _alertRepository.UpdateAsync(id, alert);
        _logger.LogInformation("Alert {id} resumed successfully.", id);
    }

    public async Task HandleCallbackAsync(string action, string parameters)
    {
        var alertId = parameters.Trim();
        switch (action)
        {
            case "pause":
                await HandlePause(alertId);
                break;

            case "resume":
                await HandleResume(alertId);
                break;

            case "delete":
                await HandleDelete(alertId);
                break;
        }
    }
}
