using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using LinkAudioPlayer.Data;

namespace LinkAudioPlayer.TestSupport;

// A real SQLite database held in memory for the lifetime of one test.
// Doubles as the context factory the stores expect, so each operation gets a
// fresh change tracker exactly as it does in the app.
public sealed class SqliteTestDatabase : IDbContextFactory<LibraryDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection) => _connection = connection;

    public static async Task<SqliteTestDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);

        var database = new SqliteTestDatabase(connection);

        await using var context = database.CreateDbContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        return database;
    }

    /// A new context over the same database.
    public LibraryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new LibraryDbContext(options);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
