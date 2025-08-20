using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class TestExecutor : BaseExecutor
{
    public TestExecutor(ILogger logger,
                        IStrategyRepository strategyRepository,
                        JavaScriptEvaluator javaScriptEvaluator,
                        IAccountProcessorFactory accountProcessorFactory,
                        GlobalState globalState)
        : base(logger, strategyRepository, javaScriptEvaluator, accountProcessorFactory, globalState)
    {
    }

    public override StrategyType StrategyType => StrategyType.CloseSell;
}

public class BaseExecutorTests
{
    private readonly Mock<ILogger<TestExecutor>> _mockLogger;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<GlobalState> _mockState;
    private readonly TestExecutor _executor;
    private readonly CancellationToken _ct;

    public BaseExecutorTests()
    {
        _mockLogger = new Mock<ILogger<TestExecutor>>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockState = new Mock<GlobalState>(Mock.Of<ILogger<GlobalState>>());
        _executor = new TestExecutor(_mockLogger.Object,
                                     _mockStrategyRepository.Object,
                                     _mockJavaScriptEvaluator.Object,
                                     _mockAccountProcessorFactory.Object,
                                     _mockState.Object);
        _ct = CancellationToken.None;
    }
    [Fact]
    public async Task Execute_ShouldCheckOrderStatusAndUpdateStrategy()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };
        _mockAccountProcessor.SetupSuccessfulPlaceLongOrderAsync(12345L);
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, CancellationToken.None);

        // Assert
        Assert.True(strategy.HasOpenOrder); // True since order status is new.
        Assert.NotNull(strategy.OrderId);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteLoop_ShouldGetStrategyFromStateManagerAndExecute()
    {
        // Arrange
        var strategy = new Strategy
        {
            Id = "test-id",
            StrategyType = StrategyType.CloseSell,
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1)); // Cancel after 1 second to end the loop

        _mockState
            .Setup(x => x.TryGetStrategy(strategy.Id, out It.Ref<Strategy?>.IsAny))
            .Callback((string key, out Strategy? a) => { a = strategy; })
            .Returns(true);

        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);
        _mockStrategyRepository.Setup(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _executor.ExecuteLoopAsync(_mockAccountProcessor.Object, strategy.Id, cts.Token);

        // Assert
        _mockState.Verify(x => x.TryGetStrategy(strategy.Id, out It.Ref<Strategy?>.IsAny), Times.AtLeastOnce);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteLoop_WhenStrategyNotFoundInStateManager_ShouldSkipExecution()
    {
        // Arrange
        var strategy = new Strategy
        {
            Id = "test-id",
            StrategyType = StrategyType.CloseSell,
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(1)); // Cancel after 1 second to end the loop

        _mockState
            .Setup(x => x.TryGetStrategy(strategy.Id, out It.Ref<Strategy?>.IsAny))
            .Callback((string key, out Strategy? a) => { a = null; })
            .Returns(false);

        // Act
        await _executor.ExecuteLoopAsync(_mockAccountProcessor.Object, strategy.Id, cts.Token);

        // Assert
        _mockState.Verify(x => x.TryGetStrategy(strategy.Id, out It.Ref<Strategy?>.IsAny), Times.AtLeastOnce);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelExistingOrder_WithNoOrderId_ShouldReturnImmediately()
    {
        // Arrange
        var strategy = new Strategy { OrderId = null };

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.CancelOrderAsync(
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task CancelExistingOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        _mockAccountProcessor.SetupSuccessfulCancelOrder();

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task CancelExistingOrder_WhenFailed_ShouldLogError()
    {
        // Arrange
        var strategy = new Strategy { OrderId = 12345 };
        var error = "Cancel order failed";

        _mockAccountProcessor.SetupFailedCancelOrder(error);

        // Act
        await _executor.CancelExistingOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, $"Failed to cancel order. Error: {error}");
    }

    [Fact]
    public async Task CheckOrderStatus_WithNoOrderId_ShouldResetHasOpenOrder()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = null,
            HasOpenOrder = true
        };

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
    }

    [Theory]
    [InlineData(OrderStatus.Filled)]
    public async Task CheckOrderStatus_WithFilledOrder_ShouldUpdateStrategy(OrderStatus status)
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        _mockAccountProcessor.SetupSuccessfulGetOrder(status);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Equal(12345, strategy.OrderId); // Order ID should remain the same
        Assert.NotNull(strategy.OrderPlacedTime);
    }

    [Theory]
    [InlineData(OrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired)]
    [InlineData(OrderStatus.Rejected)]
    public async Task CheckOrderStatus_WithFailedOrder_ShouldResetOrderStatus(OrderStatus status)
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        _mockAccountProcessor.SetupSuccessfulGetOrder(status);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        Assert.Null(strategy.OrderPlacedTime);
    }

    [Theory]
    [InlineData(OrderStatus.New)]
    [InlineData(OrderStatus.PartiallyFilled)]
    public async Task CheckOrderStatus_WithActiveOrder_FromSameDay_ShouldNotCancelOrder(OrderStatus status)
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        _mockAccountProcessor.SetupSuccessfulGetOrder(status);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.CancelOrderAsync(
            It.IsAny<string>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }
    [Fact]
    public async Task CheckOrderStatus_WithFailed_ShouldLogError()
    {
        // Arrange
        var strategy = new Strategy
        {
            OrderId = 12345,
            HasOpenOrder = true,
            OrderPlacedTime = DateTime.UtcNow
        };

        var error = "Failed to get order status";
        _mockAccountProcessor.SetupFailedGetOrder(error);

        // Act
        await _executor.CheckOrderStatus(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, $"Failed to check order status, Error: {error}");
    }

    [Fact]
    public async Task TryPlaceOrder_WhenOrderIdExists_ShouldDoNothing()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            OrderId = 12345L,
            OrderPlacedTime = DateTime.UtcNow,
            TargetPrice = 50000m
        };

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<TimeInForce>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryPlaceOrder_WhenSuccessful_ShouldUpdateStrategy()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m
        };

        _mockAccountProcessor.SetupSuccessfulPlaceLongOrderAsync(12345L);

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        Assert.True(strategy.HasOpenOrder);
        Assert.Equal(12345L, strategy.OrderId);
        Assert.NotNull(strategy.OrderPlacedTime);
    }

    [Fact]
    public async Task TryPlaceOrder_WhenFailedWithRetries_ShouldLogWarningsAndError()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m
        };
        var MAX_RETRIES = 1;

        _mockAccountProcessor.SetupFailedPlaceLongOrderAsync("Insufficient balance");

        // Act
        await _executor.TryPlaceOrder(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockLogger.VerifyLoggingTimes(LogLevel.Warning, "Retrying", Times.Exactly(MAX_RETRIES - 1));
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "Insufficient balance");
    }

    [Fact]
    public async Task TryStopOrderAsync_WithRetrySuccess_ShouldStopOrderEventually()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m,
            OrderId = 12345L,
        };
        var MAX_RETRIES = 1;

        var failCount = 0;
        _mockAccountProcessor
            .Setup(x => x.StopLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (failCount++ < MAX_RETRIES - 1)
                {
                    return new WebCallResult<BinanceOrderBase>(null, null, null, 0, null, 0, null, null, null, null,
                        ResultDataSource.Server, null, new ServerError(0, "Temporary error"));
                }
                return new WebCallResult<BinanceOrderBase>(null, null, null, 0, null, 0, null, null, null, null,
                    ResultDataSource.Server, new BinanceOrder { Id = 12345 }, null);
            });

        // Act
        await _executor.TryStopOrderAsync(_mockAccountProcessor.Object, strategy, 1000m, _ct);

        // Assert
        Assert.False(strategy.HasOpenOrder);
        Assert.Null(strategy.OrderId);
        _mockAccountProcessor.Verify(
            x => x.StopLongOrderAsync(
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<decimal>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(MAX_RETRIES));
    }
    [Fact]
    public async Task TryStopOrderAsync_WhenFailedWithRetries_ShouldLogWarningsAndError()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m,
            OrderId = 12345L,
        };
        var MAX_RETRIES = 1;

        _mockAccountProcessor.SetupFailedStopLongOrderAsync("Network error");

        // Act
        await _executor.TryStopOrderAsync(_mockAccountProcessor.Object, strategy, 4000m, _ct);

        // Assert
        _mockLogger.VerifyLoggingTimes(LogLevel.Warning, "Retrying", Times.Exactly(MAX_RETRIES - 1));
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, $"Network error");
    }
    [Fact]
    public async Task TryStopOrderAsync_WhenOrderIdIsEmpty_ShouldDoNothing()
    {
        // Arrange
        var strategy = new Strategy
        {
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            Quantity = 1.0m,
            TargetPrice = 50000m,
        };
        // Act
        await _executor.TryStopOrderAsync(_mockAccountProcessor.Object, strategy, 4000m, _ct);

        // Assert
        _mockLogger.VerifyLoggingNever(LogLevel.Warning, "Retrying");
    }
}
