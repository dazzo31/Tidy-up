using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data.Migrations;

namespace TidyUp.Tests.TestInfrastructure;

public static class TestServices
{
    /// <summary>Builds the real DI container against an isolated profile with the real migrations applied.</summary>
    public static IServiceProvider Create(IsolatedAppData appData)
    {
        var provider = ServiceConfiguration.ConfigureServices(appData.Paths);

        provider.GetRequiredService<IDatabaseMigrator>().MigrateAsync().GetAwaiter().GetResult();

        return provider;
    }
}
