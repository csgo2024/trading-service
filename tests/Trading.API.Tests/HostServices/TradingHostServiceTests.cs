using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
using Trading.Application.Services.Trading;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.API.Tests.HostServices;

public class TradingHostServiceTests : IDisposable
{
    private readonly Mock<ILogger<TradingHostService>> _loggerMock = new();
    private readonly Mock<IStrategyRepository> _strategyRepositoryMock = new();
    private readonly Mock<IStrategyTaskManager> _strategyTaskManagerMock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly TestTradingHostService _service;

    public TradingHostServiceTests()
    {
        _service = new TestTradingHostService(_loggerMock.Object,
                                              _strategyRepositoryMock.Object,
                                              _strategyTaskManagerMock.Object);
    }

    private sealed class TestTradingHostService : TradingHostService
    {
        private int _delayCallCount;
        public TestTradingHostService(
            ILogger<TradingHostService> logger,
            IStrategyRepository strategyRepository,
            IStrategyTaskManager strategyTaskManager)
            : base(logger, strategyRepository, strategyTaskManager) { }

        public override Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            _delayCallCount++;

            if (_delayCallCount >= 2)
            {
                throw new OperationCanceledException();
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddStrategiesToTaskManager()
    {
        // Arrange
        var strategies = new List<Strategy> { new Strategy { Id = "1" }, new Strategy { Id = "2" } };
        _strategyRepositoryMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategies);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.StartAsync(_cts.Token));

        foreach (var strategy in strategies)
        {
            _strategyTaskManagerMock.Verify(m => m.HandleCreatedAsync(strategy, It.IsAny<CancellationToken>()), Times.AtLeastOnce());
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenThrowException_ShouldLogErrorAndContinue()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test");
        _strategyRepositoryMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.StartAsync(_cts.Token));

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error initializing trading service")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyStrategies_ShouldNotCallAddAsync()
    {
        // Arrange
        _strategyRepositoryMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.StartAsync(_cts.Token));

        _strategyTaskManagerMock.Verify(m => m.HandleCreatedAsync(It.IsAny<Strategy>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRespectCancellation()
    {
        // Arrange
        _cts.CancelAfter(100);

        _strategyRepositoryMock.Setup(r => r.GetActiveStrategyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Assert.ThrowsAnyAsync<Exception>(() => _service.StartAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts.Dispose();
    }
}
