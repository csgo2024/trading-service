using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Shared;
using Trading.Common.JavaScript;
using Trading.Domain.Entities;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Services.Alerts;

public class AlertNotificationServiceTests
{
    private readonly Mock<ILogger<AlertNotificationService>> _loggerMock;
    private readonly Mock<IAlertRepository> _alertRepoMock;
    private readonly Mock<JavaScriptEvaluator> _jsEvaluatorMock;
    private readonly GlobalState _globalState;
    private readonly AlertNotificationService _service;

    public AlertNotificationServiceTests()
    {
        _loggerMock = new Mock<ILogger<AlertNotificationService>>();
        _alertRepoMock = new Mock<IAlertRepository>();
        _jsEvaluatorMock = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        _globalState = new GlobalState(Mock.Of<ILogger<GlobalState>>());

        _service = new AlertNotificationService(
            _loggerMock.Object,
            _alertRepoMock.Object,
            _jsEvaluatorMock.Object,
            _globalState);
    }

    private static Alert CreateAlert() => new Alert
    {
        Id = Guid.NewGuid().ToString(),
        Symbol = "BTCUSDT",
        Interval = "1m",
        Expression = "close > open",
        LastNotification = DateTime.UtcNow.AddMinutes(-2),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
    };

    private static IBinanceKline CreateKline(decimal open, decimal close, decimal high, decimal low)
    {
        var klineMock = new Mock<IBinanceKline>();
        klineMock.SetupGet(k => k.OpenPrice).Returns(open);
        klineMock.SetupGet(k => k.ClosePrice).Returns(close);
        klineMock.SetupGet(k => k.HighPrice).Returns(high);
        klineMock.SetupGet(k => k.LowPrice).Returns(low);
        return klineMock.Object;
    }

    [Fact]
    public async Task SendNotification_WhenKlineMeets_ShouldSendNotification()
    {
        // Arrange
        var alert = CreateAlert();
        var kline = CreateKline(100, 110, 115, 95);
        var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";

        _globalState.AddOrUpdateAlert(alert.Id, alert);
        _globalState.AddOrUpdateLastKline(key, kline);
        _jsEvaluatorMock.Setup(e => e.EvaluateExpression(alert.Expression, 100, 110, 115, 95)).Returns(true);
        _alertRepoMock.Setup(r => r.UpdateAsync(alert.Id, alert, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        // Act
        await _service.ProcessAlertAsync(alert, cts.Token);

        // Assert
        _alertRepoMock.Verify(r => r.UpdateAsync(alert.Id, alert, It.IsAny<CancellationToken>()), Times.Once);
        _loggerMock.VerifyLoggingOnce(LogLevel.Information, "Expression");
    }

    [Fact]
    public async Task SendNotification_WhenKlineNotMeetCondition_ShouldNotSendNotification()
    {
        // Arrange
        var alert = CreateAlert();
        var kline = CreateKline(100, 90, 105, 85);
        var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";

        _globalState.AddOrUpdateAlert(alert.Id, alert);
        _globalState.AddOrUpdateLastKline(key, kline);
        _jsEvaluatorMock.Setup(e => e.EvaluateExpression(alert.Expression, 100, 90, 105, 85)).Returns(false);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        // Act
        await _service.ProcessAlertAsync(alert, cts.Token);

        // Assert
        _alertRepoMock.Verify(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<Alert>(), It.IsAny<CancellationToken>()), Times.Never);
        _loggerMock.VerifyLoggingNever(LogLevel.Information, "");
    }

    [Fact]
    public async Task SendNotification_WhenExceptionThrown_ShouldLogError()
    {
        // Arrange
        var alert = CreateAlert();
        var key = $"{alert.Id}-{alert.Symbol}-{alert.Interval}";
        var kline = CreateKline(100, 90, 105, 85);
        _globalState.AddOrUpdateAlert(alert.Id, alert);
        _globalState.AddOrUpdateLastKline(key, kline);

        _jsEvaluatorMock
            .Setup(e => e.EvaluateExpression(alert.Expression, 100, 90, 105, 85))
            .Throws(new InvalidOperationException("Evaluation failed"));

        var cts = new CancellationTokenSource();
        cts.CancelAfter(300);

        // Act
        try
        {
            await _service.ProcessAlertAsync(alert, cts.Token);
        }
        catch (Exception)
        {
            // ignore
        }

        // Assert
        _loggerMock.VerifyLoggingTimes(LogLevel.Error, "Failed to send alert", Times.AtLeastOnce);
    }
}
