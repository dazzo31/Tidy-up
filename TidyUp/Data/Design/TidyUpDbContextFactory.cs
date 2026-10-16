using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TidyUp.Data.Design;

/// <summary>
/// Used only by `dotnet ef`. Points at a throwaway file so tooling can never touch the user's real database.
/// </summary>
public sealed class TidyUpDbContextFactory : IDesignTimeDbContextFactory<TidyUpDbContext>
{
    public TidyUpDbContext CreateDbContext(string[] args)
    {
        var scratch = Path.Combine(Path.GetTempPath(), "TidyUp_DesignTime", "design.db");
        Directory.CreateDirectory(Path.GetDirectoryName(scratch)!);

        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={scratch}")
            .Options;

        return new TidyUpDbContext(options);
    }
}
