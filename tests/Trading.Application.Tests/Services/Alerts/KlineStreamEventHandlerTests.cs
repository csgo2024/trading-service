using Moq;
using Trading.Application.Services.Alerts;
using Trading.Domain.Entities;
using Trading.Domain.Events;

namespace Trading.Application.Tests.Services.Alerts;

public class KlineStreamEventHandlerTests
{
    private readonly Mock<IKlineStreamManager> _mockStreamManager;
    private readonly KlineStreamEventHandler _handler;
    private readonly CancellationTokenSource _cts;

    public KlineStreamEventHandlerTests()
    {
        _mockStreamManager = new Mock<IKlineStreamManager>();
        _handler = new KlineStreamEventHandler(_mockStreamManager.Object);
        _cts = new CancellationTokenSource();
    }

    [Fact]
    public async Task Handle_AlertCreatedEvent_ShouldSubscribeSymbols()
    {
        // Arrange
        var alert = new Alert { Symbol = "ETHUSDT", Interval = "1h" };
        var notification = new AlertCreatedEvent(alert);
        _mockStreamManager
            .Setup(x => x.SubscribeSymbols(It.IsAny<HashSet<string>>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(notification, _cts.Token);

        // Assert
        _mockStreamManager.Verify(
            x => x.SubscribeSymbols(
                It.Is<HashSet<string>>(s => s.Contains("ETHUSDT")),
                It.Is<HashSet<string>>(i => i.Contains("1h")),
                _cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AlertResumedEvent_ShouldSubscribeSymbols()
    {
        // Arrange
        var alert = new Alert { Symbol = "ETHUSDT", Interval = "1h" };
        var notification = new AlertResumedEvent(alert);
        _mockStreamManager
            .Setup(x => x.SubscribeSymbols(It.IsAny<HashSet<string>>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(notification, _cts.Token);

        // Assert
        _mockStreamManager.Verify(
            x => x.SubscribeSymbols(
                It.Is<HashSet<string>>(s => s.Contains("ETHUSDT")),
                It.Is<HashSet<string>>(i => i.Contains("1h")),
                _cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyCreatedEvent_WithValidSymbolAndInterval_ShouldSubscribeSymbols()
    {
        // Arrange
        var strategy = new Strategy { Symbol = "BTCUSDT", Interval = "5m" };
        var notification = new StrategyCreatedEvent(strategy);
        _mockStreamManager
            .Setup(x => x.SubscribeSymbols(It.IsAny<HashSet<string>>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(notification, _cts.Token);

        // Assert
        _mockStreamManager.Verify(
            x => x.SubscribeSymbols(
                It.Is<HashSet<string>>(s => s.Contains("BTCUSDT")),
                It.Is<HashSet<string>>(i => i.Contains("5m")),
                _cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StrategyCreatedEvent_WithNullSymbolOrInterval_ShouldNotSubscribeSymbols()
    {
        // Arrange
        var strategy = new Strategy { Symbol = "", Interval = null };
        var notification = new StrategyCreatedEvent(strategy);

        // Act
        await _handler.Handle(notification, _cts.Token);

        // Assert
        _mockStreamManager.Verify(
            x => x.SubscribeSymbols(
                It.IsAny<HashSet<string>>(),
                It.IsAny<HashSet<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
