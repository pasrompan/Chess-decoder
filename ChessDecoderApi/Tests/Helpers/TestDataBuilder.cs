using ChessDecoderApi.Models;

namespace ChessDecoderApi.Tests.Helpers;

/// <summary>
/// Builder pattern for creating test data objects with consistent defaults.
/// Makes tests more readable and maintainable.
/// </summary>
public class TestDataBuilder
{
    /// <summary>
    /// Creates a test user with default values.
    /// </summary>
    public static User CreateUser(
        string id = "test-user-123",
        string email = "test@example.com",
        string name = "Test User",
        int credits = 10)
    {
        return new User
        {
            Id = id,
            Email = email,
            Name = name,
            Picture = "https://example.com/photo.jpg",
            Provider = "google",
            Credits = credits,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a test chess game with default values.
    /// </summary>
    public static ChessGame CreateChessGame(
        Guid? id = null,
        string? userId = "test-user-123",
        string pgn = "1. e4 e5 2. Nf3 Nc6",
        string title = "Test Game")
    {
        return new ChessGame
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            PgnContent = pgn,
            Title = title,
            Description = "Test chess game",
            ProcessedAt = DateTime.UtcNow,
            IsValid = true
        };
    }

    /// <summary>
    /// Creates a batch of test users.
    /// </summary>
    public static List<User> CreateUsers(int count)
    {
        var users = new List<User>();
        for (int i = 0; i < count; i++)
        {
            users.Add(CreateUser(
                id: $"test-user-{i}",
                email: $"user{i}@example.com",
                name: $"Test User {i}",
                credits: 10 + i
            ));
        }
        return users;
    }

    /// <summary>
    /// Creates a batch of test chess games for a specific user.
    /// </summary>
    public static List<ChessGame> CreateChessGames(int count, string userId = "test-user-123")
    {
        var games = new List<ChessGame>();
        for (int i = 0; i < count; i++)
        {
            games.Add(CreateChessGame(
                userId: userId,
                pgn: $"1. e4 e5 2. Nf3 Nc6 3. Bb5 a{6 + i}"
            ));
        }
        return games;
    }

    /// <summary>
    /// Creates a user with associated chess games.
    /// </summary>
    public static (User user, List<ChessGame> games) CreateUserWithGames(
        string userId = "test-user-123",
        int gameCount = 3)
    {
        var user = CreateUser(id: userId);
        var games = CreateChessGames(gameCount, userId);
        return (user, games);
    }
}

