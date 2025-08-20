using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Services.Trading;

public class StrategyTaskManagerTests
{
    private readonly Mock<ILogger<StrategyTaskManager>> _mockLogger;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<IExecutorFactory> _mockExecutorFactory;
    private readonly Mock<ITaskManager> _mockBaseTaskManager;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<BaseExecutor> _mockExecutor;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<GlobalState> _mockGlobalState;
    private readonly StrategyTaskManager _strategyTaskManager;
    private readonly Strategy _strategy;

    public StrategyTaskManagerTests()
    {
        _mockLogger = new Mock<ILogger<StrategyTaskManager>>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockExecutorFactory = new Mock<IExecutorFactory>();
        _mockBaseTaskManager = new Mock<ITaskManager>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _mockGlobalState = new Mock<GlobalState>(Mock.Of<ILogger<GlobalState>>());

        _strategyTaskManager = new StrategyTaskManager(
            _mockLogger.Object,
            _mockBaseTaskManager.Object,
            _mockGlobalState.Object,
            _mockAccountProcessorFactory.Object,
            _mockExecutorFactory.Object);

        _mockExecutor = new Mock<BaseExecutor>(
            _mockLogger.Object,
            _mockStrategyRepository.Object,
            _mockJavaScriptEvaluator.Object,
            _mockAccountProcessorFactory.Object,
            _mockGlobalState.Object);

        _strategy = new Strategy
        {
            Id = "test-id",
            AccountType = AccountType.Future,
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy
        };

        _mockAccountProcessorFactory.Setup(f => f.GetAccountProcessor(_strategy.AccountType))
            .Returns(_mockAccountProcessor.Object);

        _mockExecutorFactory.Setup(f => f.GetExecutor(_strategy.StrategyType))
            .Returns(_mockExecutor.Object);

        _mockExecutor.Setup(e => e.ExecuteLoopAsync(_mockAccountProcessor.Object, _strategy.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockExecutor.Setup(e => e.CancelExistingOrder(_mockAccountProcessor.Object, _strategy, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    }

    [Fact]
    public async Task StartAsync_ShouldAddAndStartTask_WhenNotExists()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.AddOrUpdateStrategy(_strategy.Id, _strategy)).Returns(true);

        // Act
        await _strategyTaskManager.StartAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            _strategy.Id,
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_ShouldNotStartTask_WhenAlreadyExists()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.AddOrUpdateStrategy(_strategy.Id, _strategy)).Returns(false);

        // Act
        await _strategyTaskManager.StartAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResumeAsync_ShouldAddAndStartTask_WhenNotExists()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.AddOrUpdateStrategy(_strategy.Id, _strategy)).Returns(true);

        // Act
        await _strategyTaskManager.ResumeAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            _strategy.Id,
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_ShouldNotStartTask_WhenAlreadyExists()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.AddOrUpdateStrategy(_strategy.Id, _strategy)).Returns(false);

        // Act
        await _strategyTaskManager.ResumeAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_ShouldRemoveAndStopTask_WhenExists_NoOrderId()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.TryRemoveStrategy(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(true);

        // Act
        await _strategyTaskManager.StopAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Once);
        _mockExecutor.Verify(m => m.CancelExistingOrder(It.IsAny<IAccountProcessor>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_WhenOrderIdExists_ShouldCancelOrderAndStopTask()
    {
        // Arrange
        var strategyWithOrder = new Strategy
        {
            Id = "test-id",
            AccountType = AccountType.Future,
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy,
            OrderId = 123
        };

        _mockGlobalState.Setup(s => s.TryRemoveStrategy(strategyWithOrder.Id, out It.Ref<Strategy?>.IsAny)).Returns(true);

        _mockAccountProcessorFactory.Setup(f => f.GetAccountProcessor(strategyWithOrder.AccountType))
            .Returns(_mockAccountProcessor.Object);

        _mockExecutorFactory.Setup(f => f.GetExecutor(strategyWithOrder.StrategyType))
            .Returns(_mockExecutor.Object);

        // Act
        await _strategyTaskManager.StopAsync(strategyWithOrder);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StopAsync(TaskCategory.Strategy, strategyWithOrder.Id), Times.Once);
        _mockExecutor.Verify(m => m.CancelExistingOrder(_mockAccountProcessor.Object, strategyWithOrder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenOrderIdNotExists_ShouldStopTaskWithoutCancellingOrder()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.TryRemoveStrategy(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(false);

        // Act
        await _strategyTaskManager.StopAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Never);
        _mockExecutor.Verify(m => m.CancelExistingOrder(It.IsAny<IAccountProcessor>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PauseAsync_ShouldRemoveAndStopTask_WhenExists()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.TryRemoveStrategy(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(true);

        // Act
        await _strategyTaskManager.PauseAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Once);
    }

    [Fact]
    public async Task PauseAsync_ShouldNotStopTask_WhenNotExists()
    {
        // Arrange
        _mockGlobalState.Setup(s => s.TryRemoveStrategy(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(false);

        // Act
        await _strategyTaskManager.PauseAsync(_strategy);

        // Assert
        _mockBaseTaskManager.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Never);
    }

}
