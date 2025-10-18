using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Sqlite;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Repositories;

public class SqliteChessGameRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;

    public SqliteChessGameRepositoryTests()
    {
        _dbFactory = new TestDbContextFactory();
    }

    private async Task<(SqliteChessGameRepository gameRepo, SqliteUserRepository userRepo)> CreateRepositoriesAsync()
    {
        var context = _dbFactory.CreateContext();
        var gameRepo = new SqliteChessGameRepository(context, Mock.Of<ILogger<SqliteChessGameRepository>>());
        var userRepo = new SqliteUserRepository(context, Mock.Of<ILogger<SqliteUserRepository>>());
        return (gameRepo, userRepo);
    }

    private async Task CreateTestUserAsync(SqliteUserRepository userRepo, string userId = "test-user-123")
    {
        var user = TestDataBuilder.CreateUser(id: userId, email: $"{userId}@example.com");
        await userRepo.CreateAsync(user);
    }

    [Fact]
    public async Task CreateAsync_ValidGame_CreatesAndReturnsGame()
    {
        // Arrange
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo, "test-user");
        var game = TestDataBuilder.CreateChessGame(userId: "test-user");

        // Act
        var result = await gameRepo.CreateAsync(game);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("test-user", result.UserId);
        Assert.True(result.ProcessedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateAsync_EmptyGuid_GeneratesNewGuid()
    {
        // Arrange
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo);
        var game = TestDataBuilder.CreateChessGame(id: Guid.Empty);

        // Act
        var result = await gameRepo.CreateAsync(game);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingGame_ReturnsGame()
    {
        // Arrange
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo);
        var game = TestDataBuilder.CreateChessGame();
        await gameRepo.CreateAsync(game);

        // Act
        var result = await gameRepo.GetByIdAsync(game.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(game.Id, result.Id);
        Assert.Equal(game.PgnContent, result.PgnContent);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingGame_ReturnsNull()
    {
        // Arrange
        var (gameRepo, _) = await CreateRepositoriesAsync();
        
        // Act
        var result = await gameRepo.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUserIdAsync_MultipleGames_ReturnsAllUserGames()
    {
        // Arrange
        var userId = "test-user";
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo, userId);
        await CreateTestUserAsync(userRepo, "other-user");
        
        var games = TestDataBuilder.CreateChessGames(3, userId);
        foreach (var game in games)
        {
            await gameRepo.CreateAsync(game);
        }

        // Create game for different user
        var otherGame = TestDataBuilder.CreateChessGame(userId: "other-user");
        await gameRepo.CreateAsync(otherGame);

        // Act
        var result = await gameRepo.GetByUserIdAsync(userId);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.All(result, g => Assert.Equal(userId, g.UserId));
        // Verify ordering (most recent first)
        Assert.True(result[0].ProcessedAt >= result[1].ProcessedAt);
    }

    [Fact]
    public async Task GetByUserIdAsync_NoGames_ReturnsEmptyList()
    {
        // Arrange
        var (gameRepo, _) = await CreateRepositoriesAsync();
        
        // Act
        var result = await gameRepo.GetByUserIdAsync("non-existing-user");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByUserIdPaginatedAsync_ReturnsCorrectPage()
    {
        // Arrange
        var userId = "test-user";
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo, userId);
        
        var games = TestDataBuilder.CreateChessGames(15, userId);
        foreach (var game in games)
        {
            await gameRepo.CreateAsync(game);
            await Task.Delay(1); // Ensure different ProcessedAt times
        }

        // Act
        var (page1, totalCount) = await gameRepo.GetByUserIdPaginatedAsync(userId, pageNumber: 1, pageSize: 10);
        var (page2, _) = await gameRepo.GetByUserIdPaginatedAsync(userId, pageNumber: 2, pageSize: 10);

        // Assert
        Assert.Equal(15, totalCount);
        Assert.Equal(10, page1.Count);
        Assert.Equal(5, page2.Count);
        Assert.All(page1.Concat(page2), g => Assert.Equal(userId, g.UserId));
    }

    [Fact]
    public async Task UpdateAsync_ExistingGame_UpdatesSuccessfully()
    {
        // Arrange
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo);
        var game = TestDataBuilder.CreateChessGame(pgn: "1. e4 e5");
        await gameRepo.CreateAsync(game);
        
        game.PgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bb5";
        game.Title = "Spanish Opening";

        // Act
        var result = await gameRepo.UpdateAsync(game);

        // Assert
        Assert.Equal("1. e4 e5 2. Nf3 Nc6 3. Bb5", result.PgnContent);
        Assert.Equal("Spanish Opening", result.Title);

        // Verify in database
        var retrieved = await gameRepo.GetByIdAsync(game.Id);
        Assert.Equal("1. e4 e5 2. Nf3 Nc6 3. Bb5", retrieved!.PgnContent);
    }

    [Fact]
    public async Task DeleteAsync_ExistingGame_DeletesAndReturnsTrue()
    {
        // Arrange
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo);
        var game = TestDataBuilder.CreateChessGame();
        await gameRepo.CreateAsync(game);

        // Act
        var result = await gameRepo.DeleteAsync(game.Id);

        // Assert
        Assert.True(result);
        var retrieved = await gameRepo.GetByIdAsync(game.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingGame_ReturnsFalse()
    {
        // Arrange
        var (gameRepo, _) = await CreateRepositoriesAsync();
        
        // Act
        var result = await gameRepo.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsAsync_ExistingGame_ReturnsTrue()
    {
        // Arrange
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo);
        var game = TestDataBuilder.CreateChessGame();
        await gameRepo.CreateAsync(game);

        // Act
        var result = await gameRepo.ExistsAsync(game.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingGame_ReturnsFalse()
    {
        // Arrange
        var (gameRepo, _) = await CreateRepositoriesAsync();
        
        // Act
        var result = await gameRepo.ExistsAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCountByUserIdAsync_ReturnsCorrectCount()
    {
        // Arrange
        var userId = "test-user";
        var (gameRepo, userRepo) = await CreateRepositoriesAsync();
        await CreateTestUserAsync(userRepo, userId);
        
        var games = TestDataBuilder.CreateChessGames(7, userId);
        foreach (var game in games)
        {
            await gameRepo.CreateAsync(game);
        }

        // Act
        var result = await gameRepo.GetCountByUserIdAsync(userId);

        // Assert
        Assert.Equal(7, result);
    }

    [Fact]
    public async Task GetCountByUserIdAsync_NoGames_ReturnsZero()
    {
        // Arrange
        var (gameRepo, _) = await CreateRepositoriesAsync();
        
        // Act
        var result = await gameRepo.GetCountByUserIdAsync("non-existing-user");

        // Assert
        Assert.Equal(0, result);
    }

    public void Dispose()
    {
        _dbFactory?.Dispose();
    }
}

