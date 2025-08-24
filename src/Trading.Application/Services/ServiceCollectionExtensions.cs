using Microsoft.Extensions.DependencyInjection;
using Trading.Application.DomainEventHandlers;
using Trading.Application.IntegrationEvents.EventHandlers;
using Trading.Application.Services.Alerts;
using Trading.Application.Services.Shared;
using Trading.Application.Services.Trading;
using Trading.Application.Services.Trading.Account;
using Trading.Application.Services.Trading.Executors;
using Trading.Common.JavaScript;

namespace Trading.Application.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradingServices(this IServiceCollection services)
    {
        services.AddSingleton<AlertNotificationService>();
        services.AddSingleton<DCABuyExecutor>();
        services.AddSingleton<CloseSellExecutor>();
        services.AddSingleton<CloseBuyExecutor>();
        services.AddSingleton<OpenBuyExecutor>();
        services.AddSingleton<OpenSellExecutor>();
        services.AddSingleton<FutureProcessor>();
        services.AddSingleton<IAccountProcessorFactory, AccountProcessorFactory>();
        services.AddSingleton<ITaskManager, BaseTaskManager>();
        services.AddSingleton<GlobalState>();
        services.AddSingleton<IExecutorFactory, ExecutorFactory>();
        services.AddTransient<SymbolChangedEventHandler>();
        services.AddSingleton<IAlertNotificationService, AlertNotificationService>();
        services.AddSingleton<IKlineStreamManager, KlineStreamManager>();
        services.AddSingleton<IStrategyTaskManager, StrategyTaskManager>();
        services.AddSingleton<IAlertTaskManager, AlertTaskManager>();
        services.AddSingleton<JavaScriptEvaluator>();
        services.AddSingleton<SpotProcessor>();
        services.AddSingleton<StrategyEventHandler>();
        services.AddSingleton<KlineClosedEventHandler>();
        return services;
    }

}
