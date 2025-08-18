using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Common;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.Enums;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

public class StrategyTaskManagerTests
{
    private readonly Mock<ILogger<StrategyTaskManager>> _loggerMock;
    private readonly Mock<IAccountProcessorFactory> _accountProcessorFactoryMock;
    private readonly Mock<IExecutorFactory> _executorFactoryMock;
    private readonly Mock<IBackgroundTaskManager> _backgroundTaskManagerMock;
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock;
    private readonly Mock<IAccountProcessor> _accountProcessorMock;
    private readonly Mock<BaseExecutor> _executorMock;
    private readonly Mock<JavaScriptEvaluator> _mockJavaScriptEvaluator;
    private readonly Mock<IStrategyState> _strategyStateMock;
    private readonly StrategyTaskManager _strategyTaskManager;
    private readonly Strategy _strategy;

    public StrategyTaskManagerTests()
    {
        _loggerMock = new Mock<ILogger<StrategyTaskManager>>();
        _accountProcessorFactoryMock = new Mock<IAccountProcessorFactory>();
        _executorFactoryMock = new Mock<IExecutorFactory>();
        _backgroundTaskManagerMock = new Mock<IBackgroundTaskManager>();
        _strategyRepositoryMock = new Mock<IStrategyRepository>();
        _accountProcessorMock = new Mock<IAccountProcessor>();
        _mockJavaScriptEvaluator = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _strategyStateMock = new Mock<IStrategyState>();

        _strategyTaskManager = new StrategyTaskManager(
            _loggerMock.Object,
            _backgroundTaskManagerMock.Object,
            _strategyStateMock.Object,
            _accountProcessorFactoryMock.Object,
            _executorFactoryMock.Object);

        _executorMock = new Mock<BaseExecutor>(
            _loggerMock.Object,
            _strategyRepositoryMock.Object,
            _mockJavaScriptEvaluator.Object,
            _accountProcessorFactoryMock.Object,
            _strategyStateMock.Object);

        _strategy = new Strategy
        {
            Id = "test-id",
            AccountType = AccountType.Future,
            Symbol = "BTCUSDT",
            StrategyType = StrategyType.BottomBuy
        };

        _accountProcessorFactoryMock.Setup(f => f.GetAccountProcessor(_strategy.AccountType))
            .Returns(_accountProcessorMock.Object);

        _executorFactoryMock.Setup(f => f.GetExecutor(_strategy.StrategyType))
            .Returns(_executorMock.Object);

        _executorMock.Setup(e => e.ExecuteLoopAsync(_accountProcessorMock.Object, _strategy.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _executorMock.Setup(e => e.CancelExistingOrder(_accountProcessorMock.Object, _strategy, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    }

    [Fact]
    public async Task HandleCreatedAsync_ShouldAddAndStartTask_WhenNotExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryAdd(_strategy.Id, _strategy)).Returns(true);

        // Act
        await _strategyTaskManager.HandleCreatedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            _strategy.Id,
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleCreatedAsync_ShouldNotStartTask_WhenAlreadyExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryAdd(_strategy.Id, _strategy)).Returns(false);

        // Act
        await _strategyTaskManager.HandleCreatedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleResumedAsync_ShouldAddAndStartTask_WhenNotExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryAdd(_strategy.Id, _strategy)).Returns(true);

        // Act
        await _strategyTaskManager.HandleResumedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            _strategy.Id,
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleResumedAsync_ShouldNotStartTask_WhenAlreadyExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryAdd(_strategy.Id, _strategy)).Returns(false);

        // Act
        await _strategyTaskManager.HandleResumedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StartAsync(
            TaskCategory.Strategy,
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleDeletedAsync_ShouldRemoveAndStopTask_WhenExists_NoOrderId()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryRemove(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(true);

        // Act
        await _strategyTaskManager.HandleDeletedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Once);
        _executorMock.Verify(m => m.CancelExistingOrder(It.IsAny<IAccountProcessor>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleDeletedAsync_ShouldRemoveAndStopTask_WhenExists_WithOrderId()
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

        _strategyStateMock.Setup(s => s.TryRemove(strategyWithOrder.Id, out It.Ref<Strategy?>.IsAny)).Returns(true);

        _accountProcessorFactoryMock.Setup(f => f.GetAccountProcessor(strategyWithOrder.AccountType))
            .Returns(_accountProcessorMock.Object);

        _executorFactoryMock.Setup(f => f.GetExecutor(strategyWithOrder.StrategyType))
            .Returns(_executorMock.Object);

        // Act
        await _strategyTaskManager.HandleDeletedAsync(strategyWithOrder);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StopAsync(TaskCategory.Strategy, strategyWithOrder.Id), Times.Once);
        _executorMock.Verify(m => m.CancelExistingOrder(_accountProcessorMock.Object, strategyWithOrder, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleDeletedAsync_ShouldNotStopTask_WhenNotExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryRemove(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(false);

        // Act
        await _strategyTaskManager.HandleDeletedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Never);
        _executorMock.Verify(m => m.CancelExistingOrder(It.IsAny<IAccountProcessor>(), It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandlePausedAsync_ShouldRemoveAndStopTask_WhenExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryRemove(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(true);

        // Act
        await _strategyTaskManager.HandlePausedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Once);
    }

    [Fact]
    public async Task HandlePausedAsync_ShouldNotStopTask_WhenNotExists()
    {
        // Arrange
        _strategyStateMock.Setup(s => s.TryRemove(_strategy.Id, out It.Ref<Strategy?>.IsAny)).Returns(false);

        // Act
        await _strategyTaskManager.HandlePausedAsync(_strategy);

        // Assert
        _backgroundTaskManagerMock.Verify(m => m.StopAsync(TaskCategory.Strategy, _strategy.Id), Times.Never);
    }

}
