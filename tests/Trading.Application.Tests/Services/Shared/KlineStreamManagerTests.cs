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

    [Fact]
    public async Task SubscribeSymbols_WhenNoNewSymbolsOrIntervals_ShouldSkipResubscription()
    {
        _mockState.Setup(x => x.GetAllSymbols()).Returns(["BTCUSDT"]);
        _mockState.Setup(x => x.GetAllIntervals()).Returns(["5m"]);

        using var cts = new CancellationTokenSource();
        var result = await _manager.SubscribeSymbols(["BTCUSDT"], ["5m"], cts.Token);

        Assert.True(result);
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
    [Fact]
    public async Task SubscribeSymbols_WhenForceReconnect_ShouldResubscription()
    {
        // arrange
        _mockState.Setup(x => x.GetAllSymbols()).Returns(["BTCUSDT"]);
        _mockState.Setup(x => x.GetAllIntervals()).Returns(["5m"]);
        _mockExchangeData
            .Setup(x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CallResult<UpdateSubscription>(null, null, new CantConnectError()));

        // act
        using var cts = new CancellationTokenSource();
        await _manager.SubscribeSymbols(["BTCUSDT"], ["5m"], cts.Token, true);

        // assert
        _mockExchangeData.Verify(
            x => x.SubscribeToKlineUpdatesAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<KlineInterval>>(),
                It.IsAny<Action<DataEvent<IBinanceStreamKlineData>>>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    public static TheoryData<DateTime, DateTime> GetNextReconnectTestCases()
    {
        var data = new TheoryData<DateTime, DateTime>();

        // 01:00 前 -> 当天 01:00
        data.Add(new DateTime(2025, 9, 18, 0, 0, 0, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 1, 0, 0, DateTimeKind.Utc));

        // 正好 01:00 -> 01:00
        data.Add(new DateTime(2025, 9, 18, 1, 0, 0, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 1, 0, 0, DateTimeKind.Utc));

        // 01:00 之后 -> 当天 13:00
        data.Add(new DateTime(2025, 9, 18, 1, 0, 1, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc));

        // 13:00 前 -> 当天 13:00
        data.Add(new DateTime(2025, 9, 18, 12, 59, 59, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc));

        // 正好 13:00 -> 13:00
        data.Add(new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc),
                 new DateTime(2025, 9, 18, 13, 0, 0, DateTimeKind.Utc));

        // 晚于 13:00 -> 次日 01:00
        data.Add(new DateTime(2025, 9, 18, 13, 0, 1, DateTimeKind.Utc),
                 new DateTime(2025, 9, 19, 1, 0, 0, DateTimeKind.Utc));

        // 跨年 -> 次年 01:00
        data.Add(new DateTime(2024, 12, 31, 22, 30, 0, DateTimeKind.Utc),
                 new DateTime(2025, 1, 1, 1, 0, 0, DateTimeKind.Utc));

        // 闰日 -> 次日 01:00
        data.Add(new DateTime(2020, 2, 29, 23, 59, 59, DateTimeKind.Utc),
                 new DateTime(2020, 3, 1, 1, 0, 0, DateTimeKind.Utc));

        return data;
    }

    [Theory]
    [MemberData(nameof(GetNextReconnectTestCases))]
    public void GetNextReconnectTime_ReturnsExpectedUtc(DateTime nowUtc, DateTime expectedUtc)
    {
        var result = _manager.GetNextReconnectTime(nowUtc);

        Assert.Equal(expectedUtc, result);                  // 精确等于
        Assert.Equal(DateTimeKind.Utc, result.Kind);        // Kind 为 UTC
    }

    [Fact]
    public void ReturnedTime_IsAlwaysOn9Or21AndZeroMinutesSeconds()
    {
        var now = new DateTime(2024, 12, 31, 22, 30, 0, DateTimeKind.Utc);
        var result = _manager.GetNextReconnectTime(now).AddHours(8); // UTC+8

        Assert.True(result.Hour == 9 || result.Hour == 21, $"The returned hour should be 9 or 21, but was {result.Hour}");
        Assert.Equal(0, result.Minute);
        Assert.Equal(0, result.Second);
        Assert.Equal(0, result.Millisecond);
    }
}
