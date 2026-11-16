using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinkAudioPlayer.Data;

/// <summary>Creates the SQLite file and schema on first run, and patches older files.</summary>
public static class DatabaseInitializer
{
    /// <summary>Ensures the database directory, schema and added columns exist.</summary>
    public static async Task InitializeAsync(
        IDbContextFactory<LibraryDbContext> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        await using var db = await factory.CreateDbContextAsync(cancellationToken);

        EnsureDirectoryExists(db.Database.GetConnectionString());
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await AddMissingColumnsAsync(db, cancellationToken);
    }

    /// <summary>
    /// Adds columns introduced after a database was first created.
    /// EnsureCreated only builds a schema from nothing, so an app updated in place
    /// keeps its old table and would fail on the first query touching a new column.
    /// </summary>
    private static async Task AddMissingColumnsAsync(LibraryDbContext db, CancellationToken cancellationToken)
    {
        // The SQL is written out per table rather than built from a name. Interpolating
        // an identifier into DDL is how injection gets in, even when the value is ours.
        if (!await ColumnExistsAsync(db, "Tracks", "Position", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE "Tracks" ADD COLUMN "Position" INTEGER NOT NULL DEFAULT 0;
                """,
                cancellationToken);

            // Tracks were listed newest first.
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "Tracks"
                SET "Position" = (
                    SELECT COUNT(*) FROM "Tracks" AS "other"
                    WHERE "other"."AddedAt" > "Tracks"."AddedAt"
                );
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Tracks_Position" ON "Tracks" ("Position");
                """,
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "Playlists", "Position", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE "Playlists" ADD COLUMN "Position" INTEGER NOT NULL DEFAULT 0;
                """,
                cancellationToken);

            // Playlists were listed by name.
            await db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "Playlists"
                SET "Position" = (
                    SELECT COUNT(*) FROM "Playlists" AS "other"
                    WHERE "other"."Name" < "Playlists"."Name"
                );
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Playlists_Position" ON "Playlists" ("Position");
                """,
                cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "Tracks", "FolderPath", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE "Tracks" ADD COLUMN "FolderPath" TEXT NULL;
                """,
                cancellationToken);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_Tracks_FolderPath_Position" ON "Tracks" ("FolderPath", "Position");
                """,
                cancellationToken);
        }

        // Matches what EnsureCreated builds for a new install.
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "DeviceFolders" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_DeviceFolders" PRIMARY KEY,
                "Path" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "Position" INTEGER NOT NULL
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DeviceFolders_Path" ON "DeviceFolders" ("Path");
            """,
            cancellationToken);
    }

    /// <summary>True when the table already has the column.</summary>
    private static async Task<bool> ColumnExistsAsync(
        LibraryDbContext db,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
