using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data;

namespace TidyUp.Tests.TestInfrastructure;

public static class TestServices
{
    /// <summary>Builds the real DI container against an isolated profile with the schema created.</summary>
    public static IServiceProvider Create(IsolatedAppData appData)
    {
        var provider = ServiceConfiguration.ConfigureServices(appData.Paths);

        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TidyUpDbContext>().Database.EnsureCreated();

        return provider;
    }
}
