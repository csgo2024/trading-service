using System.ComponentModel.DataAnnotations;
using MediatR;
using Moq;
using Trading.Application.Commands;
using Trading.Domain.Entities;
using Trading.Domain.Events;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using Status = Trading.Common.Enums.Status;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Commands;

public class CreateStrategyCommandHandlerTests
{
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly CreateStrategyCommandHandler _handler;

    public CreateStrategyCommandHandlerTests()
    {
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockMediator = new Mock<IMediator>();
        _handler = new CreateStrategyCommandHandler(_mockStrategyRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateStrategy()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "btcusdt",
            Amount = 100,
            Volatility = 0.1m,
            AccountType = AccountType.Spot,
            StopLossExpression = "close > open",
            StrategyType = StrategyType.OpenBuy,
            Interval = "1d"
        };

        Strategy? capturedStrategy = null;
        _mockStrategyRepository
            .Setup(x => x.AddAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .Callback<Strategy, CancellationToken>((strategy, _) => capturedStrategy = strategy)
            .ReturnsAsync((Strategy s, CancellationToken _) => s);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(capturedStrategy);

        // Verify entity properties
        Assert.Equal(command.Symbol.ToUpper(), result.Symbol);
        Assert.Equal(command.Amount, result.Amount);
        Assert.Equal(command.Volatility, result.Volatility);
        Assert.Equal(command.AccountType, result.AccountType);
        Assert.Equal(command.StrategyType, result.StrategyType);
        Assert.Equal(Status.Running, result.Status);
        Assert.True(result.CreatedAt <= DateTime.Now);
        Assert.True(result.CreatedAt > DateTime.Now.AddMinutes(-1));

        // Verify repository call
        _mockStrategyRepository.Verify(
            x => x.AddAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("", 100, 0.1, "Symbol cannot be empty")]
    [InlineData("BTCUSDT", 9, 0.1, "Amount must be greater than 10")]
    [InlineData("BTCUSDT", 100, 0.95, "Volatility must be between 0.00001 and 0.9")]
    [InlineData("BTCUSDT", 100, 0.000005, "Volatility must be between 0.00001 and 0.9")]
    public async Task Handle_WithInvalidCommand_ShouldThrowValidationException(
        string symbol, int amount, decimal Volatility, string expectedError)
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = symbol,
            Amount = amount,
            Volatility = Volatility,
            AccountType = AccountType.Spot,
            StrategyType = StrategyType.OpenBuy
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(expectedError, exception.Message);

        // Verify no repository calls or events
        _mockStrategyRepository.Verify(
            x => x.AddAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockMediator.Verify(
            x => x.Publish(It.IsAny<StrategyCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]  // Invalid leverage
    [InlineData(21)] // Too high leverage
    public async Task Handle_WithInvalidLeverage_ShouldThrowValidationException(int leverage)
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            Volatility = 0.1m,
            Leverage = leverage,
            AccountType = AccountType.Future,
            StrategyType = StrategyType.OpenBuy
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Leverage must be between 1 and 20", exception.Message);
    }
    [Theory]
    [InlineData(StrategyType.OpenSell)]
    [InlineData(StrategyType.CloseSell)]
    public async Task Handle_WhenAccountTypeIsSpot_StrategyIsSell_ShouldThrowValidationException(StrategyType strategyType)
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            Volatility = 0.1m,
            Leverage = 5,
            AccountType = AccountType.Spot,
            StopLossExpression = "close > open",
            StrategyType = strategyType
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Spot account type is not supported for OpenSell or CloseSell strategy.", exception.Message);
    }

    [Fact]
    public async Task Handle_WhenRepositoryFails_ShouldNotPublishEvent()
    {
        // Arrange
        var command = new CreateStrategyCommand
        {
            Symbol = "BTCUSDT",
            Amount = 100,
            Volatility = 0.1m,
            StrategyType = StrategyType.OpenBuy,
            Interval = "1d",
            StopLossExpression = "close > open"
        };

        var expectedException = new InvalidOperationException("Test exception");
        _mockStrategyRepository
            .Setup(x => x.AddAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Verify no events were published
        _mockMediator.Verify(
            x => x.Publish(It.IsAny<StrategyCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
