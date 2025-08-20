using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients.UsdFuturesApi;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Trading.Application.Services.Shared;
using Trading.Exchange.Binance.Wrappers.Clients;

namespace Trading.Application.Tests.Services.Shared;

public class KlineStreamManagerTests
{
    private readonly Mock<ILogger<KlineStreamManager>> _mockLogger;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<GlobalState> _mockState;
    private readonly BinanceSocketClientUsdFuturesApiWrapper _usdFutureSocketClient;
    private readonly KlineStreamManager _manager;

    private readonly Mock<IBinanceSocketClientUsdFuturesApiAccount> _mockAccount;
    private readonly Mock<IBinanceSocketClientUsdFuturesApiTrading> _mockTrading;
    private readonly Mock<IBinanceSocketClientUsdFuturesApiExchangeData> _mockExchangeData;

    public KlineStreamManagerTests()
    {
        _mockLogger = new Mock<ILogger<KlineStreamManager>>();
        _mockMediator = new Mock<IMediator>();
        _mockState = new Mock<GlobalState>(Mock.Of<ILogger<GlobalState>>());

        _mockAccount = new Mock<IBinanceSocketClientUsdFuturesApiAccount>();
        _mockTrading = new Mock<IBinanceSocketClientUsdFuturesApiTrading>();
        _mockExchangeData = new Mock<IBinanceSocketClientUsdFuturesApiExchangeData>();

        _usdFutureSocketClient = new BinanceSocketClientUsdFuturesApiWrapper(
            _mockAccount.Object,
            _mockExchangeData.Object,
            _mockTrading.Object);

        _manager = new KlineStreamManager(
            _mockLogger.Object,
            _mockMediator.Object,
            _usdFutureSocketClient,
            _mockState.Object);
        _mockState.Setup(x => x.GetAllSymbols()).Returns([]);
        _mockState.Setup(x => x.GetAllIntervals()).Returns([]);
    }

    [Fact]
    public async Task SubscribeSymbols_WithValidInput_ShouldSubscribeSuccessfully()
    {
        // Arrange
        var symbols = new HashSet<string> { "BTCUSDT" };
        var intervals = new HashSet<string> { "5m" };

        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, null));

        // Act
        using var cts = new CancellationTokenSource();
        var result = await _manager.SubscribeSymbols(symbols, intervals, cts.Token);

        // Assert
        Assert.True(result);
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.Is<IEnumerable<string>>(s => s.Contains("BTCUSDT")),
                It.Is<IEnumerable<KlineInterval>>(i => i.Contains(KlineInterval.FiveMinutes)),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubscribeSymbols_WithEmptyInput_ShouldReturnFalse()
    {
        // Arrange
        var emptySymbols = new HashSet<string>();
        var intervals = new HashSet<string> { "1m" };

        // Act
        using var cts = new CancellationTokenSource();
        var result = await _manager.SubscribeSymbols(emptySymbols, intervals, cts.Token);

        // Assert
        Assert.False(result);
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubscribeSymbols_WhenSubscriptionFails_ShouldReturnFalse()
    {
        // Arrange
        var symbols = new HashSet<string> { "BTCUSDT" };
        var intervals = new HashSet<string> { "5m" };

        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, new CantConnectError()));

        // Act
        using var cts = new CancellationTokenSource();
        var result = await _manager.SubscribeSymbols(symbols, intervals, cts.Token);

        // Assert
        Assert.False(result);
        _mockLogger.VerifyLoggingOnce(LogLevel.Error, "");
    }
}
