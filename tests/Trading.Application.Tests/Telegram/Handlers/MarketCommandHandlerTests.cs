using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types.Enums;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Telegram.Handlers;
using Trading.Common.Models;
using AccountType = Trading.Common.Enums.AccountType;

namespace Trading.Application.Tests.Telegram.Handlers;

public class MarketCommandHandlerTests
{
    private readonly Mock<ILogger<MarketCommandHandler>> _mockLogger;
    private readonly Mock<IAccountProcessorFactory> _mockAccountProcessorFactory;
    private readonly Mock<IAccountProcessor> _mockAccountProcessor;
    private readonly Mock<ITelegramBotClient> _mockBotClient;
    private readonly Mock<IOptions<TelegramSettings>> _mockSettings;
    private readonly MarketCommandHandler _handler;
    private const string ChatId = "456456481";

    public MarketCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<MarketCommandHandler>>();
        _mockAccountProcessorFactory = new Mock<IAccountProcessorFactory>();
        _mockAccountProcessor = new Mock<IAccountProcessor>();
        _mockBotClient = new Mock<ITelegramBotClient>();
        _mockSettings = new Mock<IOptions<TelegramSettings>>();

        _mockSettings.Setup(x => x.Value)
            .Returns(new TelegramSettings { ChatId = ChatId });

        _mockAccountProcessorFactory
            .Setup(x => x.GetAccountProcessor(AccountType.Future))
            .Returns(_mockAccountProcessor.Object);

        _handler = new MarketCommandHandler(
            _mockLogger.Object,
            _mockAccountProcessorFactory.Object,
            _mockBotClient.Object,
            _mockSettings.Object);
    }

    [Fact]
    public void Command_ShouldReturnCorrectValue()
    {
        Assert.Equal("/market", MarketCommandHandler.Command);
    }

    [Theory]
    [InlineData("", "BTCUSDT", "1d")]
    [InlineData("ETHUSDT", "ETHUSDT", "1d")]
    [InlineData("ETHUSDT 4h", "ETHUSDT", "4h")]
    [InlineData("eth  4h", "ETH", "4h")]
    [InlineData("  btc  1d  ", "BTC", "1d")]
    public async Task HandleAsync_ShouldParseParameters_Correctly(string input, string expectedSymbol, string expectedInterval)
    {
        // Arrange
        var dailykline = CreateMockDailyKline();
        _mockAccountProcessor.SetupSuccessfulGetKlines([dailykline], KlineInterval.OneDay);
        var fourHourkLine = CreateMock4hKline();
        _mockAccountProcessor.SetupSuccessfulGetKlines([fourHourkLine], KlineInterval.FourHour);

        // Act
        await _handler.HandleAsync(input);

        // Assert
        // Verify that GetKlines was called with the correct parameters
        _mockAccountProcessor.Verify(x => x.GetKlines(
                expectedSymbol,
                It.Is<KlineInterval>(i => i == (expectedInterval == "4h" ? KlineInterval.FourHour : KlineInterval.OneDay)),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.Is<int?>(limit => expectedInterval == "4h" ? limit == 90 : limit == 15), // 15 days for 1d, 6*15=90 for 4h
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify daily kline fetch for caption
        if (expectedInterval == "4h")
        {
            _mockAccountProcessor.Verify(x => x.GetKlines(
                    expectedSymbol,
                    It.Is<KlineInterval>(i => i == KlineInterval.OneDay),
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.Is<int?>(limit => limit == 1), // Should fetch 1 day kline for caption
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task HandleAsync_WithEmptyKlineResponse_ShouldReturnWithoutSendingMessage()
    {
        // Arrange
        _mockAccountProcessor.SetupSuccessfulGetKlines([], KlineInterval.OneDay);

        // Act
        await _handler.HandleAsync("BTCUSDT");

        // Assert
        _mockBotClient.Verify(
            x => x.SendRequest(
                It.Is<SendPhotoRequest>(y => y.ChatId == ChatId
                    && y.Caption!.Contains("BTCUSDT")
                    && y.Photo.FileType != FileType.Stream),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithFailedKlineResponse_ShouldLogError()
    {
        // Arrange
        _mockAccountProcessor.SetupFailedGetKlines();

        // Act
        await _handler.HandleAsync("BTCUSDT");

        // Assert
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "Server Error.");
    }

    [Theory]
    [InlineData(KlineInterval.OneDay)]
    [InlineData(KlineInterval.FourHour)]
    public async Task HandleAsync_WithValidKlineData_ShouldSendPhotoMessage(KlineInterval interval)
    {
        // Arrange
        var dailykline = CreateMockDailyKline();
        _mockAccountProcessor.SetupSuccessfulGetKlines([dailykline], KlineInterval.OneDay);
        var fourHourkLine = CreateMock4hKline();
        _mockAccountProcessor.SetupSuccessfulGetKlines([fourHourkLine], KlineInterval.FourHour);

        // Act
        await _handler.HandleAsync(interval == KlineInterval.FourHour ? "BTCUSDT 4h" : "BTCUSDT");

        // Assert
        _mockBotClient.Verify(
            x => x.SendRequest(
                It.Is<SendPhotoRequest>(y => y.ChatId == ChatId
                    && y.Caption!.Contains("BTCUSDT")
                    && y.Photo.FileType == FileType.Stream),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_ShouldThrowNotImplementedException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(
            () => _handler.HandleCallbackAsync("action", "parameters"));
    }

    #region Helper Methods

    private static IBinanceKline CreateMockDailyKline()
    {
        var mockKline = new Mock<IBinanceKline>();
        mockKline.SetupGet(k => k.OpenPrice).Returns(40000);
        mockKline.SetupGet(k => k.ClosePrice).Returns(41000);
        mockKline.SetupGet(k => k.HighPrice).Returns(42000);
        mockKline.SetupGet(k => k.LowPrice).Returns(39000);
        mockKline.SetupGet(k => k.OpenTime).Returns(DateTime.UtcNow.Date);
        mockKline.SetupGet(k => k.CloseTime).Returns(DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1));
        return mockKline.Object;
    }
    private static IBinanceKline CreateMock4hKline()
    {
        var mockKline = new Mock<IBinanceKline>();
        mockKline.SetupGet(k => k.OpenPrice).Returns(40000);
        mockKline.SetupGet(k => k.ClosePrice).Returns(41000);
        mockKline.SetupGet(k => k.HighPrice).Returns(42000);
        mockKline.SetupGet(k => k.LowPrice).Returns(39000);
        mockKline.SetupGet(k => k.OpenTime).Returns(DateTime.UtcNow.Date);
        mockKline.SetupGet(k => k.CloseTime).Returns(DateTime.UtcNow.Date.AddHours(4).AddSeconds(-1));
        return mockKline.Object;
    }

    #endregion
}
