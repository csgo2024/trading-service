using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Shared;
using Trading.Application.Telegram.Handlers;

namespace Trading.Application.Tests.Telegram.Handlers;

public class DebugCommandHandlerTests
{
    private readonly Mock<ILogger<DebugCommandHandler>> _mockLogger;
    private readonly DebugCommandHandler _handler;

    public DebugCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<DebugCommandHandler>>();
        var globalState = new GlobalState(Mock.Of<ILogger<GlobalState>>());
        _handler = new DebugCommandHandler(_mockLogger.Object, globalState);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSendRequest()
    {
        // arrange
        // Act
        await _handler.HandleAsync("");

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, "Debug command received");

    }
    [Fact]
    public async Task HandleCallbackAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _handler.HandleCallbackAsync("debug", ""));
    }
}
