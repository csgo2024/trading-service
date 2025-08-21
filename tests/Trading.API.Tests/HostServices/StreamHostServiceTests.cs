using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
using Trading.API.Tests;
using Trading.Application.Services.Shared;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

public class StreamHostServiceTests
{
    private readonly Mock<IAlertRepository> _alertRepoMock = new();
    private readonly Mock<IStrategyRepository> _strategyRepoMock = new();
    private readonly Mock<IKlineStreamManager> _streamManagerMock = new();
    private readonly Mock<ILogger<StreamHostService>> _loggerMock = new();

    private sealed class TestStreamHostService : StreamHostService
    {
        public bool DelayCalled { get; private set; }

        public TestStreamHostService(ILogger<StreamHostService> logger,
                                     IAlertRepository alertRepository,
                                     IStrategyRepository strategyRepository,
                                     IKlineStreamManager klineStreamManager)
            : base(logger, alertRepository, strategyRepository, klineStreamManager)
        {
        }

        public override Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            DelayCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSubscribeInitially_WhenNotSubscribed()
    {
        // Arrange
        var alerts = new List<Alert> { new() { Symbol = "BTCUSDT", Interval = "15m" } };
        var strategies = new List<Strategy> { new() { Symbol = "ETHUSDT", Interval = "5m" } };

        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);
        _strategyRepoMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(strategies);
        _streamManagerMock.Setup(m => m.SubscribeSymbols(It.IsAny<HashSet<string>>(),
                                                         It.IsAny<HashSet<string>>(),
                                                         It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);

        var service = new TestStreamHostService(_loggerMock.Object,
                                                _alertRepoMock.Object,
                                                _strategyRepoMock.Object,
                                                _streamManagerMock.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        Assert.True(service.DelayCalled);
        _streamManagerMock.Verify(m => m.SubscribeSymbols(It.IsAny<HashSet<string>>(),
                                                          It.IsAny<HashSet<string>>(),
                                                          It.IsAny<CancellationToken>()),
                                  Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotSubscribe_WhenNoSymbolsOrIntervals()
    {
        // Arrange
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync([]);
        _strategyRepoMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync([]);

        var service = new TestStreamHostService(_loggerMock.Object,
                                                _alertRepoMock.Object,
                                                _strategyRepoMock.Object,
                                                _streamManagerMock.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        _streamManagerMock.Verify(m => m.SubscribeSymbols(It.IsAny<HashSet<string>>(),
                                                          It.IsAny<HashSet<string>>(),
                                                          It.IsAny<CancellationToken>()),
                                  Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReconnect_WhenNeeded()
    {
        // Arrange
        var alerts = new List<Alert> { new() { Symbol = "BTCUSDT", Interval = "1m" } };
        var strategies = new List<Strategy> { new() { Symbol = "ETHUSDT", Interval = "5m" } };

        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);
        _strategyRepoMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync(strategies);
        _streamManagerMock.SetupSequence(m => m.NeedsReconnection())
                          .Returns(false)
                          .Returns(true);
        _streamManagerMock.Setup(m => m.SubscribeSymbols(It.IsAny<HashSet<string>>(),
                                                         It.IsAny<HashSet<string>>(),
                                                         It.IsAny<CancellationToken>()))
                          .ReturnsAsync(true);

        var service = new TestStreamHostService(_loggerMock.Object,
                                                _alertRepoMock.Object,
                                                _strategyRepoMock.Object,
                                                _streamManagerMock.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        _streamManagerMock.Verify(m => m.SubscribeSymbols(It.IsAny<HashSet<string>>(),
                                                          It.IsAny<HashSet<string>>(),
                                                          It.IsAny<CancellationToken>()),
                                  Times.AtLeast(2));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_WhenExceptionThrown()
    {
        // Arrange
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new InvalidDataException("DB error"));
        _strategyRepoMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
                         .ReturnsAsync([]);

        var service = new TestStreamHostService(_loggerMock.Object,
                                                _alertRepoMock.Object,
                                                _strategyRepoMock.Object,
                                                _streamManagerMock.Object);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        _loggerMock.VerifyLoggingTimes(LogLevel.Error, "Initial subscription failed", Times.AtLeastOnce());
    }
}
