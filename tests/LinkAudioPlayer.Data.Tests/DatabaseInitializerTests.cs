using LinkAudioPlayer.Data;
using LinkAudioPlayer.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinkAudioPlayer.Data.Tests;

// EnsureCreated only builds a schema from nothing, so an app updated in place keeps
// its old table. Without the patch step the first query touching a new column throws.
public sealed class DatabaseInitializerTests : IAsyncLifetime
{
    private SqliteTestDatabase _database = null!;

    public async ValueTask InitializeAsync() =>
        _database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Adds_the_position_column_to_a_database_that_predates_it()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var context = _database.CreateDbContext())
        {
            await DropPositionAsync(context, ct);
            Assert.False(await HasPositionAsync(context, ct));
        }

        await DatabaseInitializer.InitializeAsync(_database, ct);

        await using var check = _database.CreateDbContext();
        Assert.True(await HasPositionAsync(check, ct));
    }

    [Fact]
    public async Task Seeds_positions_on_an_upgraded_database_so_the_order_is_kept()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        var store = new TrackStore(_database);
        await store.AddAsync(Track("oldest", now.AddHours(-2)), ct);
        await store.AddAsync(Track("newest", now), ct);
        await store.AddAsync(Track("middle", now.AddHours(-1)), ct);

        await using (var seed = _database.CreateDbContext())
        {
            // Drop the column to mimic a database that never had it.
            await DropPositionAsync(seed, ct);
        }

        await DatabaseInitializer.InitializeAsync(_database, ct);

        var tracks = await new TrackStore(_database).ListAsync(ct);

        Assert.Equal(["newest", "middle", "oldest"], tracks.Select(t => t.Title));
    }

    [Fact]
    public async Task Running_twice_is_harmless()
    {
        var ct = TestContext.Current.CancellationToken;

        await DatabaseInitializer.InitializeAsync(_database, ct);
        await DatabaseInitializer.InitializeAsync(_database, ct);

        await using var check = _database.CreateDbContext();
        Assert.True(await HasPositionAsync(check, ct));
    }

    [Fact]
    public async Task Adds_the_phone_folder_schema_to_a_database_that_predates_it()
    {
        var ct = TestContext.Current.CancellationToken;

        await using (var context = _database.CreateDbContext())
        {
            await DropPhoneSchemaAsync(context, ct);
        }

        await DatabaseInitializer.InitializeAsync(_database, ct);

        var store = new DeviceFolderStore(_database);
        await store.SyncAsync([new("content://media/1", "Yellow", null, null, "Music/Coldplay")], ct);

        var folder = Assert.Single(await store.ListAsync(ct));
        Assert.Equal("Coldplay", folder.Folder.Name);
    }

    /// <summary>Drops the columns and their dependent indexes, mimicking the older schema.</summary>
    private static async Task DropPositionAsync(LibraryDbContext db, CancellationToken ct)
    {
        // The phone folder index came later and depends on Position, so it goes first.
        await DropPhoneSchemaAsync(db, ct);
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Tracks_Position\";", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Tracks\" DROP COLUMN \"Position\";", ct);
    }

    /// <summary>Removes everything the phone folders added, as an install from before them.</summary>
    private static async Task DropPhoneSchemaAsync(LibraryDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS \"IX_Tracks_FolderPath_Position\";", ct);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Tracks\" DROP COLUMN \"FolderPath\";", ct);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"DeviceFolders\";", ct);
    }

    private static async Task<bool> HasPositionAsync(LibraryDbContext db, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(\"Tracks\");";

        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (reader.GetString(1) == "Position")
            {
                return true;
            }
        }

        return false;
    }

    private static Domain.Track Track(string title, DateTimeOffset addedAt) => new()
    {
        SourceUrl = $"https://example.com/{title}",
        Provider = "test",
        Title = title,
        CacheKey = title,
        AddedAt = addedAt,
    };
}
