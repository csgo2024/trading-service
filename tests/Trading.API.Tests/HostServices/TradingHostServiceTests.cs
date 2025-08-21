using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
using Trading.API.Tests;
using Trading.Application.Services.Trading;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

public class TradingHostServiceTests
{
    private readonly Mock<ILogger<TradingHostService>> _mockLogger;
    private readonly Mock<IStrategyRepository> _mockStrategyRepository;
    private readonly Mock<IStrategyTaskManager> _mockStrategyTaskManager;
    private readonly TestTradingHostService _hostService;

    public TradingHostServiceTests()
    {
        _mockLogger = new Mock<ILogger<TradingHostService>>();
        _mockStrategyRepository = new Mock<IStrategyRepository>();
        _mockStrategyTaskManager = new Mock<IStrategyTaskManager>();

        _hostService = new TestTradingHostService(
            _mockLogger.Object,
            _mockStrategyRepository.Object,
            _mockStrategyTaskManager.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldInitializeStrategiesOnFirstRun()
    {
        // Arrange
        var strategies = new List<Strategy> { new Strategy(), new Strategy() };
        _mockStrategyRepository
            .Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategies);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200); // Stop quickly

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _mockStrategyRepository.Verify(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockStrategyTaskManager.Verify(m => m.StartAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()),
                                Times.Exactly(strategies.Count));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReinitializeAfterFirstRun()
    {
        // Arrange
        var strategies = new List<Strategy> { new Strategy() };
        _mockStrategyRepository.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategies);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _mockStrategyRepository.Verify(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_WhenRepositoryThrows()
    {
        // Arrange
        _mockStrategyRepository.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ThrowsAsync(new InvalidDataException("Repo error"));

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _mockLogger.VerifyLoggingTimes(LogLevel.Error, "Error initializing trading service", Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_WhenTaskManagerThrows()
    {
        // Arrange
        var strategies = new List<Strategy> { new Strategy() };
        _mockStrategyRepository.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(strategies);

        _mockStrategyTaskManager.Setup(m => m.StartAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidDataException("Task error"));

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _mockLogger.VerifyLoggingTimes(LogLevel.Error, "Error initializing trading service", Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallSimulateDelay()
    {
        // Arrange
        var strategies = new List<Strategy> { new Strategy() };
        _mockStrategyRepository.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(strategies);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        Assert.True(_hostService.DelayCalled);
    }

    // Helper class to override delay
    private sealed class TestTradingHostService : TradingHostService
    {
        public bool DelayCalled { get; private set; }

        public TestTradingHostService(ILogger<TradingHostService> logger,
                                      IStrategyRepository repo,
                                      IStrategyTaskManager taskManager)
            : base(logger, repo, taskManager) { }

        public override Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            DelayCalled = true;
            return Task.CompletedTask; // no real delay
        }
    }
}
