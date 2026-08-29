using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicPlayer.Data;

namespace MusicPlayer.TestSupport;

// A real SQLite database held in memory for the lifetime of one test.
// Hands out a fresh DbContext per call so tests see the same per request
// isolation the server has, instead of one long lived change tracker.
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection) => _connection = connection;

    public static async Task<SqliteTestDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);

        var database = new SqliteTestDatabase(connection);

        await using var context = database.NewContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        return database;
    }

    /// A new context over the same database. Dispose it when the operation is done.
    public LibraryDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new LibraryDbContext(options);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
