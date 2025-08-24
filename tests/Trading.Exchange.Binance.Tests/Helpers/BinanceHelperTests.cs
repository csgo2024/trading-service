using Binance.Net.Objects.Models.Spot;
using Trading.Exchange.Binance.Helpers;

namespace Trading.Exchange.Binance.Tests.Helpers;

public class BinanceHelperTests
{
    [Fact]
    public void AdjustPriceByStepSize_WhenFilterIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            BinanceHelper.AdjustPriceByStepSize(100m, null));

        Assert.Equal("filter", exception.ParamName);
    }

    [Theory]
    [InlineData(10.123456, 0.01, 10.12)]
    [InlineData(10.125, 0.01, 10.12)]
    [InlineData(10.155, 0.01, 10.15)]
    [InlineData(10.159, 0.01, 10.15)]
    public void AdjustPriceByStepSize_WithValidInput_ShouldRoundCorrectly(
        decimal price, decimal tickSize, decimal expected)
    {
        // Arrange
        var filter = new BinanceSymbolPriceFilter
        {
            TickSize = tickSize,
            MinPrice = 0.01m,
            MaxPrice = 100000m
        };

        // Act
        var result = BinanceHelper.AdjustPriceByStepSize(price, filter);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.009, "Price must be greater than 0.01")]
    [InlineData(100001, "Price must be less than 100000")]
    public void AdjustPriceByStepSize_WithInvalidPrice_ShouldThrowException(
        decimal price, string expectedMessage)
    {
        // Arrange
        var filter = new BinanceSymbolPriceFilter
        {
            TickSize = 0.01m,
            MinPrice = 0.01m,
            MaxPrice = 100000m
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BinanceHelper.AdjustPriceByStepSize(price, filter));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void AdjustQuantityBystepSize_WhenFilterIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            BinanceHelper.AdjustQuantityBystepSize(100m, null));

        Assert.Equal("filter", exception.ParamName);
    }

    [Theory]
    [InlineData(1.123456, 0.01, 1.12)]
    [InlineData(1.125, 0.01, 1.12)]
    [InlineData(1.155, 0.01, 1.15)]
    [InlineData(1.159, 0.01, 1.15)]
    public void AdjustQuantityBystepSize_WithValidInput_ShouldRoundCorrectly(
        decimal quantity, decimal stepSize, decimal expected)
    {
        // Arrange
        var filter = new BinanceSymbolLotSizeFilter
        {
            StepSize = stepSize,
            MinQuantity = 0.01m,
            MaxQuantity = 10000m
        };

        // Act
        var result = BinanceHelper.AdjustQuantityBystepSize(quantity, filter);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.009, "Quantity must be greater than 0.01")]
    [InlineData(10001, "Quantity must be less than 10000")]
    public void AdjustQuantityBystepSize_WithInvalidQuantity_ShouldThrowException(
        decimal quantity, string expectedMessage)
    {
        // Arrange
        var filter = new BinanceSymbolLotSizeFilter
        {
            StepSize = 0.01m,
            MinQuantity = 0.01m,
            MaxQuantity = 10000m
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            BinanceHelper.AdjustQuantityBystepSize(quantity, filter));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void GetKLinePeriod_ShouldReturnCorrectMinutePeriod()
    {
        var utcTime = new DateTime(2025, 8, 24, 10, 15, 30, DateTimeKind.Utc);
        var interval = "15m"; // 15-minute interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        // Check that the start time is 10:15:00 and the end time is 10:30:00
        var expectedStart = new DateTime(2025, 8, 24, 10, 15, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 8, 24, 10, 30, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }
    [Fact]
    public void GetKLinePeriod_ShouldReturnCorrectHourPeriod()
    {
        var utcTime = new DateTime(2025, 8, 24, 10, 15, 30, DateTimeKind.Utc);
        var interval = "1h"; // 1-hour interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        // The expected start time should be the top of the hour, i.e., 10:00:00
        var expectedStart = new DateTime(2025, 8, 24, 10, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 8, 24, 11, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }

    [Fact]
    public void GetKLinePeriod_ShouldReturnCorrectDayPeriod()
    {
        var utcTime = new DateTime(2025, 8, 24, 10, 15, 30, DateTimeKind.Utc);
        var interval = "1d"; // 1-day interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        // The expected start time should be the start of the day: 2025-08-24 00:00:00
        var expectedStart = new DateTime(2025, 8, 24, 0, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 8, 25, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }
    [Fact]
    public void GetKLinePeriod_ShouldReturnCorrectWeekPeriod()
    {
        var utcTime = new DateTime(2025, 8, 24, 10, 15, 30, DateTimeKind.Utc);
        var interval = "1w"; // 1-week interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        // The week starts on Monday, so for 2025-08-24 (Sunday), the week starts on 2025-08-18 (Monday)
        var expectedStart = new DateTime(2025, 8, 18, 0, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 8, 25, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }
    [Fact]
    public void GetKLinePeriod_ShouldReturnCorrectMultipleWeeksPeriod()
    {
        var utcTime = new DateTime(2025, 8, 24, 10, 15, 30, DateTimeKind.Utc);
        var interval = "2w"; // 2-week interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        // For 2025-08-24 (Sunday), the first week starts on 2025-08-18, and 2 weeks later the period should end on 2025-09-01
        var expectedStart = new DateTime(2025, 8, 18, 0, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }
    [Fact]
    public void GetKLinePeriod_ShouldThrowArgumentExceptionForInvalidInterval()
    {
        var utcTime = new DateTime(2025, 8, 24, 10, 15, 30, DateTimeKind.Utc);
        var interval = "xyz"; // Invalid interval
        var exception = Assert.Throws<FormatException>(() => BinanceHelper.GetKLinePeriod(utcTime, interval));
    }
    [Fact]
    public void GetKLinePeriod_ShouldHandleMonthTransitionCorrectly()
    {
        var utcTime = new DateTime(2025, 8, 31, 23, 59, 59, DateTimeKind.Utc);
        var interval = "1d"; // 1-day interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        // Transitioning from August to September, start should be 2025-08-31 00:00:00, end should be 2025-09-01 00:00:00
        var expectedStart = new DateTime(2025, 8, 31, 0, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }
    [Fact]
    public void GetKLinePeriod_ShouldHandleMidnightEdgeCase()
    {
        var utcTime = new DateTime(2025, 8, 24, 0, 0, 0, DateTimeKind.Utc);
        var interval = "1d"; // 1-day interval
        var result = BinanceHelper.GetKLinePeriod(utcTime, interval);

        var expectedStart = new DateTime(2025, 8, 24, 0, 0, 0, DateTimeKind.Utc);
        var expectedEnd = new DateTime(2025, 8, 25, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expectedStart, result.Open);
        Assert.Equal(expectedEnd, result.Close);
    }
}
