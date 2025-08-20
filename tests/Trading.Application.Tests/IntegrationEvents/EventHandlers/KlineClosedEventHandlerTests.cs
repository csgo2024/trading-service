using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.IntegrationEvents.EventHandlers;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;
using AccountType = Trading.Common.Enums.AccountType;
using StrategyType = Trading.Common.Enums.StrategyType;

namespace Trading.Application.Tests.IntegrationEvents.EventHandlers;

public class KlineClosedEventHandlerTests
{
    private readonly Mock<ILogger<KlineClosedEventHandler>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<GlobalState> _mockState;
    private readonly Mock<IExecutorFactory> _mockExecutorFactory;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<BaseExecutor> _mockExecutor;
    private readonly KlineClosedEventHandler _handler;

    public KlineClosedEventHandlerTests()
    {
        _mockLogger = new Mock<ILogger<KlineClosedEventHandler>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockState = new Mock<GlobalState>(Mock.Of<ILogger<GlobalState>>());
        _mockExecutorFactory = new Mock<IExecutorFactory>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        var mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockExecutor = new Mock<BaseExecutor>(
            Mock.Of<ILogger>(),
            Mock.Of<IStrategyRepository>(),
            mockJavaScriptEvaluator.Object,
            Mock.Of<IAccountProcessorFactory>(),
            _mockState.Object
        );

        _handler = new KlineClosedEventHandler(
            _mockLogger.Object,
            _mockStrategyRepository.Object,
            _mockAccountProcessorFactory.Object,
            _mockState.Object,
            _mockExecutorFactory.Object
        );
    }

    [Fact]
    public async Task Handle_WithNoMatchingStrategies_ShouldNotExecuteAnything()
    {
        // Arrange
        var mockKline = new Mock<IBinanceKline>();
        mockKline.Setup(k => k.ClosePrice).Returns(1000m);

        var klineEvent = new KlineClosedEvent("BTCUSDT", KlineInterval.OneHour, mockKline.Object);

        _mockState.Setup(x => x.GetAllStrategies())
            .Returns([]);

        // Act
        await _handler.Handle(klineEvent, CancellationToken.None);

        // Assert
        _mockExecutorFactory.Verify(x => x.GetExecutor(It.IsAny<StrategyType>()), Times.Never);
        _mockAccountProcessorFactory.Verify(x => x.GetAccountProcessor(It.IsAny<AccountType>()), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMatchingStrategy_ShouldExecuteAndUpdateStrategy()
    {
        // Arrange
        var mockKline = new Mock<IBinanceKline>();
        mockKline.Setup(k => k.ClosePrice).Returns(1000m);

        var klineEvent = new KlineClosedEvent("BTCUSDT", KlineInterval.OneHour, mockKline.Object);

        var strategy = new Strategy
        {
            Id = "test-strategy",
            Symbol = "BTCUSDT",
            Interval = "1h",
            StrategyType = StrategyType.CloseBuy,
            AccountType = AccountType.Spot
        };

        _mockState.Setup(x => x.GetAllStrategies())
            .Returns([strategy]);

        _mockExecutorFactory.Setup(x => x.GetExecutor(StrategyType.CloseBuy))
            .Returns(_mockExecutor.Object);

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(AccountType.Spot))
            .Returns(_mockAccountProcessor.Object);

        _mockExecutor.Setup(x => x.ShouldStopLoss(strategy, klineEvent))
            .Returns(false);

        // Act
        await _handler.Handle(klineEvent, CancellationToken.None);

        // Assert
        _mockExecutor.Verify(x => x.HandleKlineClosedEvent(_mockAccountProcessor.Object, strategy, klineEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockExecutor.Verify(x => x.TryStopOrderAsync(It.IsAny<IAccountProcessor>(), It.IsAny<Strategy>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(strategy.Id, strategy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMatchingStrategyAndStopLoss_ShouldExecuteStopLossAndPauseStrategy()
    {
        // Arrange
        var mockKline = new Mock<IBinanceKline>();
        mockKline.Setup(k => k.ClosePrice).Returns(1000m);

        var klineEvent = new KlineClosedEvent("BTCUSDT", KlineInterval.OneHour, mockKline.Object);

        var strategy = new Strategy
        {
            Id = "test-strategy",
            Symbol = "BTCUSDT",
            Interval = "1h",
            StrategyType = StrategyType.CloseBuy,
            AccountType = AccountType.Spot,
            Status = Status.Running
        };

        _mockState.Setup(x => x.GetAllStrategies())
            .Returns([strategy]);

        _mockExecutorFactory.Setup(x => x.GetExecutor(StrategyType.CloseBuy))
            .Returns(_mockExecutor.Object);

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(AccountType.Spot))
            .Returns(_mockAccountProcessor.Object);

        _mockExecutor.Setup(x => x.ShouldStopLoss(strategy, klineEvent))
            .Returns(true);

        // Act
        await _handler.Handle(klineEvent, CancellationToken.None);

        // Assert
        _mockExecutor.Verify(x => x.HandleKlineClosedEvent(_mockAccountProcessor.Object, strategy, klineEvent, It.IsAny<CancellationToken>()), Times.Once);
        _mockExecutor.Verify(x => x.TryStopOrderAsync(_mockAccountProcessor.Object, strategy, 1000m, It.IsAny<CancellationToken>()), Times.Once);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(strategy.Id, strategy, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(Status.Paused, strategy.Status);
    }

    [Fact]
    public async Task Handle_WithInvalidExecutorOrProcessor_ShouldSkipExecution()
    {
        // Arrange
        var mockKline = new Mock<IBinanceKline>();
        mockKline.Setup(k => k.ClosePrice).Returns(1000m);

        var klineEvent = new KlineClosedEvent("BTCUSDT", KlineInterval.OneHour, mockKline.Object);

        var strategy = new Strategy
        {
            Id = "test-strategy",
            Symbol = "BTCUSDT",
            Interval = "1h",
            StrategyType = StrategyType.CloseBuy,
            AccountType = AccountType.Spot
        };

        _mockState.Setup(x => x.GetAllStrategies())
            .Returns([strategy]);

        _mockExecutorFactory.Setup(x => x.GetExecutor(StrategyType.CloseBuy))
            .Returns(null as BaseExecutor);

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(AccountType.Spot))
            .Returns(_mockAccountProcessor.Object);

        // Act
        await _handler.Handle(klineEvent, CancellationToken.None);

        // Assert
        _mockExecutor.Verify(x => x.HandleKlineClosedEvent(It.IsAny<IAccountProcessor>(), It.IsAny<Strategy>(), It.IsAny<KlineClosedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMultipleStrategies_ShouldHandleAllInParallel()
    {
        // Arrange
        var mockKline = new Mock<IBinanceKline>();
        mockKline.Setup(k => k.ClosePrice).Returns(1000m);

        var klineEvent = new KlineClosedEvent("BTCUSDT", KlineInterval.OneHour, mockKline.Object);

        var strategies = new[]
        {
            new Strategy
            {
                Id = "strategy-1",
                Symbol = "BTCUSDT",
                Interval = "1h",
                StrategyType = StrategyType.CloseBuy,
                AccountType = AccountType.Spot
            },
            new Strategy
            {
                Id = "strategy-2",
                Symbol = "BTCUSDT",
                Interval = "1h",
                StrategyType = StrategyType.CloseSell,
                AccountType = AccountType.Future
            }
        };

        _mockState.Setup(x => x.GetAllStrategies())
            .Returns(strategies);

        _mockExecutorFactory.Setup(x => x.GetExecutor(It.IsAny<StrategyType>()))
            .Returns(_mockExecutor.Object);

        _mockAccountProcessorFactory.Setup(x => x.GetAccountProcessor(It.IsAny<AccountType>()))
            .Returns(_mockAccountProcessor.Object);

        _mockExecutor.Setup(x => x.ShouldStopLoss(It.IsAny<Strategy>(), klineEvent))
            .Returns(false);

        // Act
        await _handler.Handle(klineEvent, CancellationToken.None);

        // Assert
        _mockExecutor.Verify(x => x.HandleKlineClosedEvent(_mockAccountProcessor.Object, It.IsAny<Strategy>(), klineEvent, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockStrategyRepository.Verify(x => x.UpdateAsync(It.IsAny<string>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
