namespace TidyUp.Tests.TestInfrastructure;

internal static class AppSourceFiles
{
    /// <summary>Resolves a path relative to the TidyUp project directory (e.g. "Styles/Colors.xaml").</summary>
    public static string Find(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "TidyUp", relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate TidyUp/{relativePath} above {AppContext.BaseDirectory}");
    }
}
