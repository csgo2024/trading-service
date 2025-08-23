using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot.Types.Enums;
using Trading.Application.Commands;
using Trading.Application.Telegram.Handlers;
using Trading.Application.Telegram.Logging;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram.Handlers;

public class StrategyCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<StrategyCommandHandler>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly StrategyCommandHandler _handler;

    public StrategyCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<StrategyCommandHandler>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();

        _handler = new StrategyCommandHandler(
            _mockMediator.Object,
            _mockLogger.Object,
            _mockStrategyRepository.Object);
    }

    [Fact]
    public void Command_ShouldReturnCorrectValue()
    {
        Assert.Equal("/strategy", StrategyCommandHandler.Command);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyParameters_LogInformation_WhenNoStrategies()
    {
        // arrange
        _mockStrategyRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync([]);
        // Act
        await _handler.HandleAsync("");

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, "Strategy is empty, please create and call later.");
    }

    [Theory]
    [InlineData(Status.Running, "Running")]
    [InlineData(Status.Paused, "Paused")]
    public async Task HandleAsync_WithEmptyParameters_ShouldReturnStrategyInformation(Status status, string statusText)
    {
        // arrange
        _mockStrategyRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync([new Strategy()
                {
                    Symbol = "BTCUSDT",
                    AccountType = AccountType.Spot,
                    Status = status,
                }
            ]);
        // Act
        await _handler.HandleAsync("");

        // Assert
        _mockLogger.Verify(
            x => x.BeginScope(
                It.Is<TelegramLoggerScope>(x => x.ParseMode == ParseMode.Html)),
            Times.Once);
        _mockLogger.VerifyLoggingOnce(LogLevel.Information, statusText);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidSubCommand_ShouldLogError()
    {
        // Act
        await _handler.HandleAsync("invalid xyz");

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "Unknown command. Use: create, delete, pause, or resume");
    }

    [Fact]
    public async Task HandleCreate_WithValidJson_ShouldSendCommand()
    {
        // Arrange
        var json = """
            {
                "symbol": "BTCUSDT",
                "accountType": 0,
                "amount": 100,
                "Volatility": 0.01,
                "strategyType": 0
            }
            """;

        // Act
        await _handler.HandleAsync($"create {json}");

        // Assert
        _mockMediator.Verify(
            x => x.Send(
                It.Is<CreateStrategyCommand>(cmd =>
                    cmd.Symbol == "BTCUSDT"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCreate_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = "invalid json";

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(
            () => _handler.HandleAsync($"create {invalidJson}"));
    }

    [Fact]
    public async Task HandleCreate_WithEmptyJson_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.HandleAsync("create "));
    }

    [Fact]
    public async Task HandleDelete_WithValidId_ShouldSendCommand()
    {
        // Arrange
        const string strategyId = "test-id";
        _mockMediator
            .Setup(x => x.Send(It.IsAny<DeleteStrategyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"delete {strategyId}");

        // Assert
        _mockMediator.Verify(
            x => x.Send(
                It.Is<DeleteStrategyCommand>(cmd => cmd.Id == strategyId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleDelete_WithEmptyId_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.HandleAsync("delete "));
    }

    [Fact]
    public async Task HandleDelete_WhenDeleteFails_ShouldThrowInvalidOperationException()
    {
        // Arrange
        const string strategyId = "test-id";
        _mockMediator
            .Setup(x => x.Send(It.IsAny<DeleteStrategyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync($"delete {strategyId}"));
        Assert.Equal($"Failed to delete strategy {strategyId}", exception.Message);
    }

    [Fact]
    public async Task HandlePause_WithValidId_ShouldUpdateStatus()
    {
        // Arrange
        const string strategyId = "test-id";
        _mockStrategyRepository
            .Setup(x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Strategy()
            {
                Id = strategyId,
                Symbol = "BTCUSDT",
                AccountType = AccountType.Spot,
            });

        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(strategyId, It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"pause {strategyId}");

        // Assert
        _mockStrategyRepository.Verify(
            x => x.UpdateAsync(
                strategyId,
                It.Is<Strategy>(x => x.Status == Status.Paused),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleResume_WithValidId_ShouldUpdateStatus()
    {
        // Arrange
        const string strategyId = "test-id";
        var strategy = new Strategy { Id = strategyId, Symbol = "BTCUSDT", AccountType = AccountType.Spot, };

        _mockStrategyRepository
            .Setup(x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);
        _mockStrategyRepository
            .Setup(x => x.UpdateAsync(strategyId, It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.HandleAsync($"resume {strategyId}");

        // Assert
        _mockStrategyRepository.Verify(
            x => x.GetByIdAsync(strategyId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockStrategyRepository.Verify(
            x => x.UpdateAsync(
                strategyId,
                It.Is<Strategy>(x => x.Status == Status.Running),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
