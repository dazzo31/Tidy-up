using Microsoft.Data.Sqlite;
using TidyUp.Configuration;

namespace TidyUp.Tests.TestInfrastructure;

/// <summary>A throwaway app-data profile under %TEMP%, deleted on dispose.</summary>
public sealed class IsolatedAppData : IDisposable
{
    public IsolatedAppData()
    {
        var root = Path.Combine(Path.GetTempPath(), $"TidyUp_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Paths = new TidyUpPaths(root);
    }

    public TidyUpPaths Paths { get; }

    public void Dispose()
    {
        // SQLite pools keep file handles open past context disposal.
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(Paths.AppDataRoot))
            {
                Directory.Delete(Paths.AppDataRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Leaked temp dirs are harmless; failing the test over cleanup is not.
        }
    }
}
