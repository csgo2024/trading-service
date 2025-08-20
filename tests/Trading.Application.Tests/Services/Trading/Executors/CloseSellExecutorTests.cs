using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.Services.Trading.Executors;

public class CloseSellExecutorTests
{
    private readonly Mock<ILogger<CloseSellExecutor>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<GlobalState> _mockState;
    private readonly CloseSellExecutor _executor;
    private readonly CancellationToken _ct;

    public CloseSellExecutorTests()
    {
        _mockLogger = new Mock<ILogger<CloseSellExecutor>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockState = new Mock<GlobalState>(Mock.Of<ILogger<GlobalState>>());
        _executor = new CloseSellExecutor(
            _mockLogger.Object,
            _mockAccountProcessorFactory.Object,
            _mockStrategyRepository.Object,
            _mockJavaScriptEvaluator.Object,
            _mockState.Object
        );
        _ct = CancellationToken.None;
    }
    private static KlineClosedEvent SetupKlineCloseEvent()
    {
        var symbol = "BTCUSDT";
        var interval = KlineInterval.OneDay;
        var kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
        var @event = new KlineClosedEvent(symbol, interval, kline);
        return @event;
    }

    [Fact]
    public void StrategyType_ShouldBeCloseSell()
    {
        Assert.Equal(StrategyType.CloseSell, _executor.StrategyType);
    }

    [Fact]
    public async Task HandleKlineClosedEvent_WhenTypeIsSpot_ShouldDoNothing()
    {
        // Arrange
        var klineEvent = SetupKlineCloseEvent();
        var strategy = new Strategy
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Volatility = 0.01m,
            Amount = 1000,
            HasOpenOrder = false,
            StrategyType = StrategyType.CloseSell,
            AccountType = AccountType.Spot,
            Interval = "1d"
        };
        // Act
        await _executor.HandleKlineClosedEvent(_mockAccountProcessor.Object, strategy, klineEvent, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceShortOrderAsync(
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            It.IsAny<TimeInForce>(),
            It.IsAny<CancellationToken>()),
        Times.Never);
    }

    [Fact]
    public async Task HandleKlineClosedEvent_WhenOrderIdIsNull_ShouldUpdateStrategyAndPlaceOrder()
    {
        var klineEvent = SetupKlineCloseEvent();
        var strategy = new Strategy
        {
            Id = "test-id",
            Symbol = "BTCUSDT",
            Volatility = 0.01m,
            Amount = 1000,
            HasOpenOrder = false,
            StrategyType = StrategyType.CloseSell,
            AccountType = AccountType.Future,
            Interval = "1d"
        };
        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(12345L);
        // Act
        await _executor.HandleKlineClosedEvent(_mockAccountProcessor.Object, strategy, klineEvent, _ct);

        // Assert
        Assert.Equal(41000m, strategy.OpenPrice);
        Assert.Equal(41410m, strategy.TargetPrice); // 41000 * (1 + 0.01)
        _mockAccountProcessor.Verify(x => x.GetSymbolFilterData(strategy, _ct), Times.Once);
        _mockAccountProcessor.Verify(x => x.PlaceShortOrderAsync(
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            It.IsAny<TimeInForce>(),
            It.IsAny<CancellationToken>()),
        Times.Once);
    }

    [Theory]
    [InlineData(null, 0, 0)]
    [InlineData(100, 1, 0)]
    [InlineData(1, 0, 1)]
    public async Task ExecuteAsync_WhenStrategyIsInvalid_ShouldNotPlaceOrder(int? openPrice, int targetPrice, int amount)
    {
        // Arrange
        var strategy = new Strategy
        {
            AccountType = AccountType.Future,
            OpenPrice = openPrice,
            TargetPrice = targetPrice,
            Quantity = amount
        };

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, _ct);

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
    public async Task ExecuteAsync_WhenStrategyIsValidButNoOrder_ShouldTryPlaceOrder()
    {
        // Arrange
        var strategy = new Strategy
        {
            OpenPrice = 1000m,
            TargetPrice = 990m,
            Quantity = 0.1m,
            AccountType = AccountType.Future,
            StrategyType = StrategyType.CloseSell,
            OrderId = null
        };
        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);
        _mockAccountProcessor.SetupSuccessfulSymbolFilter();
        _mockAccountProcessor.SetupSuccessfulPlaceShortOrderAsync(12345L);
        _mockAccountProcessor.SetupSuccessfulGetOrder(OrderStatus.New);

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceShortOrderAsync(
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            It.IsAny<TimeInForce>(),
            It.IsAny<CancellationToken>()),
        Times.Once);
        _mockAccountProcessor.Verify(x => x.GetOrder(
            It.IsAny<string>(),
            It.IsAny<long?>(),
            It.IsAny<CancellationToken>()),
        Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOrderExists_ShouldNotPlaceNewOrder()
    {
        // Arrange
        var strategy = new Strategy
        {
            OpenPrice = 1000m,
            TargetPrice = 990m,
            Quantity = 0.1m,
            OrderId = 12345
        };

        // Act
        await _executor.ExecuteAsync(_mockAccountProcessor.Object, strategy, _ct);

        // Assert
        _mockAccountProcessor.Verify(x => x.PlaceShortOrderAsync(
            It.IsAny<string>(),
            It.IsAny<decimal>(),
            It.IsAny<decimal>(),
            It.IsAny<TimeInForce>(),
            It.IsAny<CancellationToken>()),
        Times.Never);
    }
}
