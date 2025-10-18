using ChessDecoderApi.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChessDecoderApi.Tests.Helpers;

/// <summary>
/// Factory for creating in-memory SQLite database contexts for testing.
/// Each context gets a unique in-memory database that is isolated from other tests.
/// </summary>
public class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ChessDecoderDbContext> _options;

    public TestDbContextFactory()
    {
        // Create in-memory SQLite connection
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Configure DbContext to use the in-memory connection
        _options = new DbContextOptionsBuilder<ChessDecoderDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Create the database schema
        using var context = new ChessDecoderDbContext(_options);
        context.Database.EnsureCreated();
    }

    /// <summary>
    /// Creates a new DbContext instance using the in-memory database.
    /// </summary>
    public ChessDecoderDbContext CreateContext()
    {
        return new ChessDecoderDbContext(_options);
    }

    /// <summary>
    /// Gets the DbContextOptions for manual context creation.
    /// </summary>
    public DbContextOptions<ChessDecoderDbContext> Options => _options;

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

