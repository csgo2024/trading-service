using Microsoft.Extensions.Logging;
using Moq;
using Trading.API.HostServices;
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

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

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

        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _alertRepoMock.Verify(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLogError_WhenRepositoryThrows()
    {
        // Arrange
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ThrowsAsync(new InvalidDataException("Repo error"));

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _loggerMock.VerifyLog(LogLevel.Error, "Error initializing alertHost service", Times.AtLeastOnce());
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

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        _loggerMock.VerifyLog(LogLevel.Error, "Error initializing alertHost service", Times.AtLeastOnce());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCallSimulateDelay()
    {
        // Arrange
        var alerts = new[] { new Alert() };
        _alertRepoMock.Setup(r => r.GetActiveAlertsAsync(It.IsAny<CancellationToken>()))
                      .ReturnsAsync(alerts);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(200);

        // Act
        await _hostService.StartAsync(cts.Token);

        // Assert
        Assert.True(_hostService.DelayCalled);
    }

    // 子类: 重写延迟方法以避免真实等待
    private sealed class TestAlertHostService : AlertHostService
    {
        public bool DelayCalled { get; private set; }

        public TestAlertHostService(ILogger<AlertHostService> logger,
                                    IAlertRepository repo,
                                    IAlertTaskManager taskManager)
            : base(logger, repo, taskManager) { }

        public override Task SimulateDelay(TimeSpan delay, CancellationToken cancellationToken)
        {
            DelayCalled = true;
            return Task.CompletedTask;
        }
    }
}
