using Trading.Application.Telegram.Logging;

namespace Trading.API;

public class Program
{
    public static void Main(string[] args)
    {
        ILogger? logger = null;
        try
        {
            var host = CreateHostBuilder(args).Build();
            logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogNotification(LogLevel.Information, null, false, null, "Starting trading-service host...");
            host.Run();
        }
        catch (Exception ex)
        {
            logger?.LogErrorNotification(ex, "Host terminated unexpectedly");
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}
