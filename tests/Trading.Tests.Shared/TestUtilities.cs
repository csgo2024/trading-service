using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Trading.Tests.Shared;

public static class TestUtilities
{
    public static IStringLocalizer<T> SetupLocalizer<T>()
    {
        var serviceCollection = new ServiceCollection();
        return SetupLocalizer<T>(serviceCollection);
    }

    public static IStringLocalizer<T> SetupLocalizer<T>(ServiceCollection serviceCollection)
    {
        serviceCollection.AddLogging();
        serviceCollection.AddLocalization(options => options.ResourcesPath = "Resources/Localization");
        var provider = serviceCollection.BuildServiceProvider();
        var localizer = provider.GetRequiredService<IStringLocalizer<T>>();
        return localizer;
    }
}
