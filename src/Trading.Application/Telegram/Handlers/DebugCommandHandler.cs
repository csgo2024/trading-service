using Microsoft.Extensions.Logging;
using Trading.Application.Services.Shared;

namespace Trading.Application.Telegram.Handlers;

public class DebugCommandHandler : ICommandHandler
{
    private readonly ILogger<DebugCommandHandler> _logger;
    private readonly GlobalState _globalState;
    public static string Command => "/debug";
    public static string CallbackPrefix => "debug";

    public DebugCommandHandler(
        ILogger<DebugCommandHandler> logger,
        GlobalState globalState)
    {
        _logger = logger;
        _globalState = globalState;
    }

    public async Task HandleAsync(string parameters)
    {
        _logger.LogInformation("Debug command received. Current global state: {@State}", _globalState);
        await Task.Delay(100);
    }

    public Task HandleCallbackAsync(string action, string parameters)
    {
        throw new NotImplementedException();
    }
}
