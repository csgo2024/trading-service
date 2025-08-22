using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Shared;
using Trading.Application.Telegram;
using Trading.Application.Telegram.Handlers;
using Trading.Common.JavaScript;
using Trading.Domain.IRepositories;

namespace Trading.Application.Tests.Telegram;

public class TelegramCommandHandlerFactoryTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramCommandHandlerFactory _factory;

    public TelegramCommandHandlerFactoryTests()
    {
        var services = new ServiceCollection();

        // Add logging services
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        // Add mock dependencies
        services.AddSingleton(Mock.Of<IMediator>());
        services.AddSingleton(Mock.Of<IStrategyRepository>());
        services.AddSingleton(Mock.Of<IAlertRepository>());
        services.AddSingleton(Mock.Of<ITelegramBotClient>()); // Add TelegramBotClient mock
        services.AddSingleton(Mock.Of<IAlertNotificationService>()); // Add TelegramBotClient mock

        var mockState = new Mock<GlobalState>(Mock.Of<ILogger<GlobalState>>());
        services.AddSingleton(mockState);

        var jsEvaluatorMock = new Mock<JavaScriptEvaluator>(Mock.Of<ILogger<JavaScriptEvaluator>>());
        services.AddSingleton(jsEvaluatorMock.Object);

        services.AddTransient<HelpCommandHandler>();
        services.AddTransient<StrategyCommandHandler>();
        services.AddTransient<AlertCommandHandler>();

        _serviceProvider = services.BuildServiceProvider();
        _factory = new TelegramCommandHandlerFactory(_serviceProvider);
    }

    [Theory]
    [InlineData("/help", typeof(HelpCommandHandler))]
    [InlineData("/alert", typeof(AlertCommandHandler))]
    [InlineData("/strategy", typeof(StrategyCommandHandler))]
    [InlineData("alert", typeof(AlertCommandHandler))]
    [InlineData("strategy", typeof(StrategyCommandHandler))]
    public void GetHandler_ShouldReturnCorrectHandler(string command, Type expectedType)
    {
        // Act
        var handler = _factory.GetHandler(command);

        // Assert
        Assert.NotNull(handler);
        Assert.IsType(expectedType, handler);
    }

    [Fact]
    public void GetHandler_WithInvalidCommand_ShouldReturnNull()
    {
        // Act
        var handler = _factory.GetHandler("/invalid");

        // Assert
        Assert.Null(handler);
    }
}
