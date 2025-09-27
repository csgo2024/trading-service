using Trading.API.HostServices;
using Trading.Application.Commands;
using Trading.Application.Middlewares;
using Trading.Application.Queries;
using Trading.Application.Services;
using Trading.Application.Telegram;
using Trading.Common.Services;
using Trading.Infrastructure;

namespace Trading.API;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder =>
                {
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            );
        });

        // Add Localization
        services.AddLocalization(options => options.ResourcesPath = "Resources/Localization");
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[] { "en-US", "zh-CN" };
            options.SetDefaultCulture(supportedCultures[0])
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);
        });
        // Register Language Service
        services.AddSingleton<ILanguageService, LanguageService>();

        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddSingleton<IErrorMessageResolver, DefaultErrorMessageResolver>();

        services.AddMongoDb(Configuration);
        services.AddTelegram(Configuration);

        services.AddScoped<IStrategyQuery, StrategyQuery>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining(typeof(CreateAlertCommand));
            cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
        });
        services.AddHostedService<AlertHostService>();
        services.AddHostedService<TradingHostService>();
        services.AddHostedService<StreamHostService>();
        services.AddHostedService<TelegramBotService>();
        services.AddTradingServices();
        Exchange.Binance.ServiceCollectionExtensions.AddBinance(services, Configuration);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseCors("AllowAll");
        app.UseExceptionHandlingMiddleware();
        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseRouting();

        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
