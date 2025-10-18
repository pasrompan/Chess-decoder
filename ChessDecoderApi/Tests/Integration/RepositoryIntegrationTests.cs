using ChessDecoderApi.Repositories.Sqlite;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Integration;

/// <summary>
/// Integration tests for repository implementations using real in-memory SQLite database
/// </summary>
public class RepositoryIntegrationTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;

    public RepositoryIntegrationTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    [Fact]
    public async Task UserRepository_FullCrudWorkflow_WorksCorrectly()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var logger = Mock.Of<ILogger<SqliteUserRepository>>();
        var repository = new SqliteUserRepository(context, logger);

        var user = TestDataBuilder.CreateUser(
            id: "integration-test-user",
            email: "integration@example.com",
            credits: 100
        );

        // Act & Assert - Create
        var created = await repository.CreateAsync(user);
        Assert.NotNull(created);
        Assert.Equal("integration@example.com", created.Email);

        // Act & Assert - Read by ID
        var retrieved = await repository.GetByIdAsync(user.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(user.Email, retrieved.Email);

        // Act & Assert - Read by Email
        var retrievedByEmail = await repository.GetByEmailAsync(user.Email);
        Assert.NotNull(retrievedByEmail);
        Assert.Equal(user.Id, retrievedByEmail.Id);

        // Act & Assert - Update
        retrieved!.Name = "Updated Name";
        await repository.UpdateAsync(retrieved);
        var updated = await repository.GetByIdAsync(user.Id);
        Assert.Equal("Updated Name", updated!.Name);

        // Act & Assert - Credits operations
        var credits = await repository.GetCreditsAsync(user.Id);
        Assert.Equal(100, credits);

        await repository.UpdateCreditsAsync(user.Id, 50);
        var updatedCredits = await repository.GetCreditsAsync(user.Id);
        Assert.Equal(50, updatedCredits);

        // Act & Assert - Exists
        var exists = await repository.ExistsAsync(user.Id);
        Assert.True(exists);

        // Act & Assert - Delete
        var deleted = await repository.DeleteAsync(user.Id);
        Assert.True(deleted);

        var existsAfterDelete = await repository.ExistsAsync(user.Id);
        Assert.False(existsAfterDelete);
    }

    [Fact]
    public async Task ChessGameRepository_WithMultipleUsers_IsolatesDataCorrectly()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var logger = Mock.Of<ILogger<SqliteChessGameRepository>>();
        var repository = new SqliteChessGameRepository(context, logger);

        var user1Games = TestDataBuilder.CreateChessGames(3, "user-1");
        var user2Games = TestDataBuilder.CreateChessGames(2, "user-2");

        // Act - Create games for both users
        foreach (var game in user1Games)
        {
            await repository.CreateAsync(game);
        }
        foreach (var game in user2Games)
        {
            await repository.CreateAsync(game);
        }

        // Assert - Each user sees only their games
        var user1Retrieved = await repository.GetByUserIdAsync("user-1");
        Assert.Equal(3, user1Retrieved.Count);
        Assert.All(user1Retrieved, g => Assert.Equal("user-1", g.UserId));

        var user2Retrieved = await repository.GetByUserIdAsync("user-2");
        Assert.Equal(2, user2Retrieved.Count);
        Assert.All(user2Retrieved, g => Assert.Equal("user-2", g.UserId));

        // Assert - Pagination works correctly
        var (page1, totalCount) = await repository.GetByUserIdPaginatedAsync("user-1", 1, 2);
        Assert.Equal(3, totalCount);
        Assert.Equal(2, page1.Count);

        var (page2, _) = await repository.GetByUserIdPaginatedAsync("user-1", 2, 2);
        Assert.Single(page2);
    }

    [Fact]
    public async Task MultipleRepositories_ShareSameDatabase_MaintainConsistency()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());
        var gameRepo = new SqliteChessGameRepository(context, Mock.Of<ILogger<SqliteChessGameRepository>>());

        // Act - Create user
        var user = TestDataBuilder.CreateUser(id: "test-user", credits: 50);
        await userRepo.CreateAsync(user);

        // Act - Create games for user
        var games = TestDataBuilder.CreateChessGames(5, user.Id);
        foreach (var game in games)
        {
            await gameRepo.CreateAsync(game);
        }

        // Assert - Both repositories can access the data
        var retrievedUser = await userRepo.GetByIdAsync(user.Id);
        Assert.NotNull(retrievedUser);

        var retrievedGames = await gameRepo.GetByUserIdAsync(user.Id);
        Assert.Equal(5, retrievedGames.Count);

        var gameCount = await gameRepo.GetCountByUserIdAsync(user.Id);
        Assert.Equal(5, gameCount);
    }

    [Fact]
    public async Task ConcurrentOperations_WorkCorrectly()
    {
        // Arrange
        var context = _dbFactory.CreateContext();
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());

        // Act - Create multiple users concurrently
        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            var user = TestDataBuilder.CreateUser(
                id: $"concurrent-user-{i}",
                email: $"concurrent{i}@example.com"
            );
            return await userRepo.CreateAsync(user);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(10, results.Length);
        Assert.All(results, user => Assert.NotNull(user));

        // Verify all users exist
        for (int i = 1; i <= 10; i++)
        {
            var exists = await userRepo.ExistsAsync($"concurrent-user-{i}");
            Assert.True(exists);
        }
    }

    public void Dispose()
    {
        _dbFactory?.Dispose();
    }
}

