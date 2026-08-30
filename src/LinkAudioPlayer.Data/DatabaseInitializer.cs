using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkAudioPlayer.Data;

/// <summary>Creates the SQLite file and schema on first run.</summary>
public static class DatabaseInitializer
{
    /// <summary>Ensures the database directory and schema exist. Safe to call on every startup.</summary>
    public static async Task InitializeAsync(
        IDbContextFactory<LibraryDbContext> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        EnsureDirectoryExists(db.Database.GetConnectionString());
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>SQLite fails rather than creating missing parent directories, so create them here.</summary>
    private static void EnsureDirectoryExists(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
