using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.IntegrationEvents.EventHandlers;
using Trading.Application.IntegrationEvents.Events;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Shared;
using Trading.Common.Enums;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.IntegrationEvents.EventHandlers;

public class KlineClosedEventHandler2Tests
{
    private readonly Mock<IAlertNotificationService> _notificationServiceMock;
    private readonly Mock<IAlertRepository> _alertRepositoryMock;
    private readonly GlobalState _globalState;
    private readonly KlineClosedEventHandler2 _handler;

    public KlineClosedEventHandler2Tests()
    {
        _notificationServiceMock = new Mock<IAlertNotificationService>();
        _alertRepositoryMock = new Mock<IAlertRepository>();
        _globalState = new GlobalState(Mock.Of<ILogger<GlobalState>>());

        _handler = new KlineClosedEventHandler2(
            _notificationServiceMock.Object,
            _alertRepositoryMock.Object,
            _globalState);
    }

    private static Alert CreateAlert(string symbol, string interval, Status status = Status.Running)
    {
        return new Alert
        {
            Id = Guid.NewGuid().ToString(),
            Symbol = symbol,
            Interval = interval,
            Status = status
        };
    }

    private static IBinanceKline CreateKline()
    {
        var klineMock = new Mock<IBinanceKline>();
        klineMock.SetupGet(k => k.OpenPrice).Returns(100);
        klineMock.SetupGet(k => k.ClosePrice).Returns(110);
        klineMock.SetupGet(k => k.HighPrice).Returns(115);
        klineMock.SetupGet(k => k.LowPrice).Returns(95);
        return klineMock.Object;
    }

    [Fact]
    public async Task Handle_ShouldSendNotification_WhenAlertIsActiveAndMatchesEvent()
    {
        // Arrange
        var alert = CreateAlert("BTCUSDT", "5m", Status.Running);
        var kline = CreateKline();
        var @event = new KlineClosedEvent("BTCUSDT", KlineInterval.FiveMinutes, kline);

        _alertRepositoryMock.Setup(r => r.GetAllAlerts()).ReturnsAsync([alert]);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";
        Assert.True(_globalState.TryGetLastKline(key, out var storedKline));
        Assert.Equal(kline, storedKline);
        _notificationServiceMock.Verify(s => s.SendNotification(alert, kline, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldResumeAlert_WhenAlertIsPausedAndMatchesEvent()
    {
        // Arrange
        var alert = CreateAlert("BTCUSDT", "5m", Status.Paused);
        var kline = CreateKline();
        var @event = new KlineClosedEvent("BTCUSDT", KlineInterval.FiveMinutes, kline);

        _alertRepositoryMock.Setup(r => r.GetAllAlerts()).ReturnsAsync([alert]);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        Assert.Equal(Status.Running, alert.Status);
        _alertRepositoryMock.Verify(r => r.UpdateAsync(alert.Id, alert, It.IsAny<CancellationToken>()), Times.Once);
        _notificationServiceMock.Verify(s => s.SendNotification(It.IsAny<Alert>(), kline, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldNotSendNotificationOrUpdate_WhenNoMatchingAlertExists()
    {
        // Arrange
        var alert = CreateAlert("ETHUSDT", "5m", Status.Running);
        var kline = CreateKline();
        var @event = new KlineClosedEvent("BTCUSDT", KlineInterval.FiveMinutes, kline);

        // symbol not match
        _alertRepositoryMock.Setup(r => r.GetAllAlerts()).ReturnsAsync([alert]);

        // Act
        await _handler.Handle(@event, CancellationToken.None);

        // Assert
        _notificationServiceMock.Verify(s => s.ProcessAlertAsync(
            It.IsAny<Alert>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _alertRepositoryMock.Verify(r => r.UpdateAsync(
            It.IsAny<string>(),
            It.IsAny<Alert>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
