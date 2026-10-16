using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using TidyUp.Configuration;

namespace TidyUp.Tests.TestInfrastructure;

internal static class TestEnvironmentBootstrap
{
    // Process-wide backstop: any code path that resolves TidyUpPaths from the
    // environment lands in a temp dir, never in the developer's real %APPDATA%.
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void RedirectAppDataToTemp()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TidyUp_TestRun_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(TidyUpPaths.EnvironmentVariableName, root);

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Best-effort cleanup at process exit.
            }
        };
    }
}
