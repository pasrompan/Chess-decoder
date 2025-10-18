using ChessDecoderApi.Models;
using ChessDecoderApi.Repositories.Sqlite;
using ChessDecoderApi.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Repositories;

public class SqliteUserRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _dbFactory;
    private readonly SqliteUserRepository _repository;
    private readonly Mock<ILogger<SqliteUserRepository>> _loggerMock;

    public SqliteUserRepositoryTests()
    {
        _dbFactory = new TestDbContextFactory();
        _loggerMock = new Mock<ILogger<SqliteUserRepository>>();
        _repository = new SqliteUserRepository(_dbFactory.CreateContext(), _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ValidUser_CreatesAndReturnsUser()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: "new-user", email: "new@example.com");

        // Act
        var result = await _repository.CreateAsync(user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("new-user", result.Id);
        Assert.Equal("new@example.com", result.Email);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
        Assert.True(result.LastLoginAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser();
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingUser_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync("non-existing-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(email: "findme@example.com");
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.GetByEmailAsync("findme@example.com");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("findme@example.com", result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistingEmail_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByEmailAsync("nonexisting@example.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_ExistingUser_UpdatesSuccessfully()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser();
        await _repository.CreateAsync(user);
        
        user.Name = "Updated Name";
        user.Credits = 50;

        // Act
        var result = await _repository.UpdateAsync(user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.Equal(50, result.Credits);

        // Verify in database
        var retrieved = await _repository.GetByIdAsync(user.Id);
        Assert.Equal("Updated Name", retrieved!.Name);
        Assert.Equal(50, retrieved.Credits);
    }

    [Fact]
    public async Task DeleteAsync_ExistingUser_DeletesAndReturnsTrue()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser();
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.DeleteAsync(user.Id);

        // Assert
        Assert.True(result);
        var retrieved = await _repository.GetByIdAsync(user.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingUser_ReturnsFalse()
    {
        // Act
        var result = await _repository.DeleteAsync("non-existing-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsAsync_ExistingUser_ReturnsTrue()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser();
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.ExistsAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_NonExistingUser_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsAsync("non-existing-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCreditsAsync_ExistingUser_ReturnsCredits()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(credits: 25);
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.GetCreditsAsync(user.Id);

        // Assert
        Assert.Equal(25, result);
    }

    [Fact]
    public async Task GetCreditsAsync_NonExistingUser_ReturnsZero()
    {
        // Act
        var result = await _repository.GetCreditsAsync("non-existing-id");

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task UpdateCreditsAsync_ExistingUser_UpdatesAndReturnsTrue()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(credits: 10);
        await _repository.CreateAsync(user);

        // Act
        var result = await _repository.UpdateCreditsAsync(user.Id, 100);

        // Assert
        Assert.True(result);
        var credits = await _repository.GetCreditsAsync(user.Id);
        Assert.Equal(100, credits);
    }

    [Fact]
    public async Task UpdateCreditsAsync_NonExistingUser_ReturnsFalse()
    {
        // Act
        var result = await _repository.UpdateCreditsAsync("non-existing-id", 50);

        // Assert
        Assert.False(result);
    }

    public void Dispose()
    {
        _dbFactory?.Dispose();
    }
}

