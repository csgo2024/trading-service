using Microsoft.Extensions.Logging;
using Moq;

namespace Trading.API.Tests;

public static class TestExtensions
{
    public static void VerifyLoggingTimes<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage, Times time) where T : class
    {
        logger.Verify(
            x => x.Log(
                It.Is<LogLevel>(x => x == logLevel),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            time);
    }
    public static void VerifyLoggingTimes<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage, Func<Times> time) where T : class
    {
        VerifyLoggingTimes(logger, logLevel, expectedMessage, time());
    }
    public static void VerifyLoggingNever<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage = "") where T : class
    {
        VerifyLoggingTimes(logger, logLevel, expectedMessage, Times.Never);
    }
    public static void VerifyLoggingOnce<T>(this Mock<ILogger<T>> logger, LogLevel logLevel, string expectedMessage) where T : class
    {
        VerifyLoggingTimes(logger, logLevel, expectedMessage, Times.Once);
    }
}
