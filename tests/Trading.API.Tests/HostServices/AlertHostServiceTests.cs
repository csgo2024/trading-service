using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
using Trading.API.Tests;
using Trading.Application.Services.Alerts;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

public class AlertHostServiceTests
{
    private readonly Mock<ILogger<AlertHostService>> _loggerMock;
    private readonly Mock<IAlertRepository> _alertRepoMock;
    private readonly Mock<IAlertTaskManager> _taskManagerMock;
    private readonly TestAlertHostService _hostService;

    public AlertHostServiceTests()
    {
        _loggerMock = new Mock<ILogger<AlertHostService>>();
        _alertRepoMock = new Mock<IAlertRepository>();
        _taskManagerMock = new Mock<IAlertTaskManager>();

        _hostService = new TestAlertHostService(
            _loggerMock.Object,
            _alertRepoMock.Object,
            _taskManagerMock.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldInitializeAlertsOnFirstRun()
    {
        // Arrange
        var alerts = new List<Alert> { new Alert(), new Alert() };
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);

        // Act
        await _hostService.StartAsync(CancellationToken.None);
        await _hostService.DelayCalledTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _alertRepoMock.Verify(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _taskManagerMock.Verify(m => m.StartAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()),
                                Times.Exactly(alerts.Count));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReinitializeAfterFirstRun()
    {
        // Arrange
        var alerts = new[] { new Alert() };
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);

        // Act
        await _hostService.StartAsync(CancellationToken.None);
        await _hostService.DelayCalledTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _alertRepoMock.Verify(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_WhenRepositoryThrows()
    {
        // Arrange
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new InvalidDataException("Repo error"));

        // Act
        await _hostService.StartAsync(CancellationToken.None);
        await _hostService.DelayCalledTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _loggerMock.VerifyLoggingTimes(LogLevel.Error, "Error initializing alertHost service", Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_WhenTaskManagerThrows()
    {
        // Arrange
        var alerts = new[] { new Alert() };
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);

        _taskManagerMock.Setup(m => m.StartAsync(It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidDataException("Task error"));

        // Act
        await _hostService.StartAsync(CancellationToken.None);
        await _hostService.DelayCalledTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        _loggerMock.VerifyLoggingTimes(LogLevel.Error, "Error initializing alertHost service", Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallSimulateDelay()
    {
        // Arrange
        var alerts = new[] { new Alert() };
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);

        // Act
        await _hostService.StartAsync(CancellationToken.None);
        await _hostService.DelayCalledTask.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.True(_hostService.DelayCalledTask.IsCompleted);
    }

    // 子类: 重写延迟方法以避免真实等待
    private sealed class TestAlertHostService : AlertHostService
    {
        private readonly TaskCompletionSource _delayCalledTcs = new();
        public Task DelayCalledTask => _delayCalledTcs.Task;

        public TestAlertHostService(ILogger<AlertHostService> logger,
                                    IAlertRepository repo,
                                    IAlertTaskManager taskManager)
            : base(logger, repo, taskManager) { }

        public override Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (!_delayCalledTcs.Task.IsCompleted)
                _delayCalledTcs.TrySetResult();

            return Task.CompletedTask; // no real delay        }
        }
    }
}
