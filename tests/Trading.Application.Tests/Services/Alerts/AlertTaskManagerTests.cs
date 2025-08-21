using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Shared;
using Trading.Common.Enums;
using Trading.Domain.Entities;

namespace Trading.Application.Tests.Services.Alerts;

public class AlertTaskManagerTests
{
    private readonly Mock<ILogger<AlertTaskManager>> _loggerMock;
    private readonly Mock<ITaskManager> _taskManagerMock;
    private readonly Mock<IAlertNotificationService> _notificationServiceMock;
    private readonly GlobalState _globalState;
    private readonly AlertTaskManager _manager;
    private readonly IBinanceKline _kline;

    public AlertTaskManagerTests()
    {
        _loggerMock = new Mock<ILogger<AlertTaskManager>>();
        _taskManagerMock = new Mock<ITaskManager>();
        _notificationServiceMock = new Mock<IAlertNotificationService>();
        _globalState = new GlobalState(Mock.Of<ILogger<GlobalState>>());

        _manager = new AlertTaskManager(
            _loggerMock.Object,
            _taskManagerMock.Object,
            _notificationServiceMock.Object,
            _globalState);
        _kline = Mock.Of<IBinanceKline>(k =>
            k.OpenPrice == 40000m &&
            k.ClosePrice == 41000m &&
            k.HighPrice == 42000m &&
            k.LowPrice == 39000m);
    }

    private static Alert CreateAlert() => new Alert
    {
        Id = Guid.NewGuid().ToString(),
        Symbol = "BTCUSDT",
        Interval = "1m"
    };

    [Fact]
    public async Task StartAsync_WhenCalled_AddsAlertAndStartsTask()
    {
        // Arrange
        var alert = CreateAlert();

        // Act
        await _manager.StartAsync(alert);

        // Assert
        Assert.True(_globalState.TryGetAlert(alert.Id, out _));
        _taskManagerMock.Verify(m => m.StartAsync(
            TaskCategory.Alert,
            alert.Id,
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WhenCalled_RemovesAlertAndStopsTask()
    {
        // Arrange
        var alert = CreateAlert();
        _globalState.AddOrUpdateAlert(alert.Id, alert);
        _globalState.AddOrUpdateLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}", _kline);

        // Act
        await _manager.StopAsync(alert);

        // Assert
        Assert.False(_globalState.TryGetAlert(alert.Id, out _));
        Assert.False(_globalState.TryGetLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}", out _));
        _taskManagerMock.Verify(m => m.StopAsync(TaskCategory.Alert, alert.Id), Times.Once);
    }

    [Fact]
    public async Task PauseAsync_WhenCalled_UpdatesAlertAndStopsTask()
    {
        // Arrange
        var alert = CreateAlert();
        _globalState.AddOrUpdateLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}", _kline);

        // Act
        await _manager.PauseAsync(alert);

        // Assert
        Assert.True(_globalState.TryGetAlert(alert.Id, out _));
        Assert.False(_globalState.TryGetLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}", out _));
        _taskManagerMock.Verify(m => m.StopAsync(TaskCategory.Alert, alert.Id), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_WhenCalled_UpdatesAlertAndStartsTask()
    {
        // Arrange
        var alert = CreateAlert();

        // Act
        await _manager.ResumeAsync(alert);

        // Assert
        Assert.True(_globalState.TryGetAlert(alert.Id, out _));
        _taskManagerMock.Verify(m => m.StartAsync(
            TaskCategory.Alert,
            alert.Id,
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EmptyAsync_WhenCalled_ClearsStateAndStopsAllTasks()
    {
        // Arrange
        var alert = CreateAlert();
        _globalState.AddOrUpdateAlert(alert.Id, alert);
        _globalState.AddOrUpdateLastKline($"{alert.Id}-{alert.Symbol}-{alert.Interval}", _kline);

        // Act
        await _manager.EmptyAsync(CancellationToken.None);

        // Assert
        _taskManagerMock.Verify(m => m.StopAsync(TaskCategory.Alert), Times.Once);
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, "Alerts emptyed");
    }
}
