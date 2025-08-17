using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class StrategyStateManagerTests
{
    private readonly Mock<ILogger<StrategyStateManager>> _mockLogger;
    private readonly StrategyStateManager _manager;

    public StrategyStateManagerTests()
    {
        _mockLogger = new Mock<ILogger<StrategyStateManager>>();
        _manager = new StrategyStateManager(_mockLogger.Object);
    }

    [Fact]
    public void GetStrategy_WithExistingStrategy_ShouldReturnStrategy()
    {
        // Arrange
        var strategy = new Strategy { Id = "1", StrategyType = StrategyType.CloseSell };
        var strategies = new Dictionary<string, Strategy> { [strategy.Id] = strategy };
        _manager.SetState(strategy.StrategyType, strategies);

        // Act
        var result = _manager.GetStrategy(strategy.StrategyType, strategy.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(strategy.Id, result.Id);
    }

    [Fact]
    public void GetStrategy_WithNonExistentStrategy_ShouldReturnNull()
    {
        // Arrange
        var strategyType = StrategyType.CloseSell;
        var strategyId = "1";

        // Act
        var result = _manager.GetStrategy(strategyType, strategyId);

        // Assert
        Assert.Null(result);
        _mockLogger.VerifyLoggingOnce(LogLevel.Warning, $"[{strategyType}] Strategy with ID {strategyId} not found in state.");
    }

    [Fact]
    public void GetStrategy_WithNonExistentStrategyType_ShouldReturnNull()
    {
        // Arrange
        var strategyType = StrategyType.CloseSell;
        var strategyId = "1";
        _manager.SetState(StrategyType.BottomBuy, new Dictionary<string, Strategy>());

        // Act
        var result = _manager.GetStrategy(strategyType, strategyId);

        // Assert
        Assert.Null(result);
        _mockLogger.VerifyLoggingOnce(LogLevel.Warning, $"[{strategyType}] Strategy with ID {strategyId} not found in state.");
    }
}
