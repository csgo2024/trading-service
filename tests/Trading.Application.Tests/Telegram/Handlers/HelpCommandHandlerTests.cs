using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Types.Enums;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.Logging;

namespace Trading.Application.Tests.Telegram.Handlers;

public class HelpCommandHandlerTests
{
    private readonly Mock<ILogger<HelpCommandHandler>> _mockLogger;
    private readonly HelpCommandHandler _handler;
    public HelpCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<HelpCommandHandler>>();

        _handler = new HelpCommandHandler(_mockLogger.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCallSendRequest()
    {
        // arrange
        // Act
        await _handler.HandleAsync("");

        // Assert
        _mockLogger.Verify(
            x => x.BeginScope(
                It.Is<TelegramLoggerScope>(x => x.ParseMode == ParseMode.None)),
            Times.Once);
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, "https://csgo2024.github.io/trading-service/");

    }
    [Fact]
    public async Task HandleCallbackAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _handler.HandleCallbackAsync("create", "123"));
    }
}
