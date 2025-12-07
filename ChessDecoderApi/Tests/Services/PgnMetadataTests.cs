using ChessDecoderApi.DTOs;
using ChessDecoderApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services;

public class PgnMetadataTests
{
    private readonly ImageProcessingService _service;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<ImageProcessingService>> _loggerMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<ChessMoveProcessor>> _chessMoveProcessorLoggerMock;
    private readonly Mock<ILogger<ChessMoveValidator>> _chessMoveValidatorLoggerMock;
    private readonly IChessMoveProcessor _chessMoveProcessor;
    private readonly IChessMoveValidator _chessMoveValidator;

    public PgnMetadataTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ImageProcessingService>>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _chessMoveProcessorLoggerMock = new Mock<ILogger<ChessMoveProcessor>>();
        _chessMoveValidatorLoggerMock = new Mock<ILogger<ChessMoveValidator>>();
        _chessMoveProcessor = new ChessMoveProcessor(_chessMoveProcessorLoggerMock.Object);
        _chessMoveValidator = new ChessMoveValidator(_chessMoveValidatorLoggerMock.Object);

        _service = new ImageProcessingService(
            _httpClientFactoryMock.Object,
            _configurationMock.Object,
            _loggerMock.Object,
            _loggerFactoryMock.Object,
            _chessMoveProcessor,
            _chessMoveValidator);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithAllMetadata_ShouldIncludeAllFields()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4", "Nf3" };
        var blackMoves = new List<string> { "e5", "Nc6" };
        var metadata = new PgnMetadata
        {
            WhitePlayer = "John Doe",
            BlackPlayer = "Jane Smith",
            GameDate = new DateTime(2025, 12, 7),
            Round = "1"
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.Contains("[Date \"2025.12.07\"]", result);
        Assert.Contains("[Round \"1\"]", result);
        Assert.Contains("[White \"John Doe\"]", result);
        Assert.Contains("[Black \"Jane Smith\"]", result);
        Assert.Contains("[Result \"*\"]", result);
        Assert.Contains("1. e4 e5", result);
        Assert.Contains("2. Nf3 Nc6", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithPartialMetadata_ShouldIncludeOnlyProvidedFields()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };
        var metadata = new PgnMetadata
        {
            WhitePlayer = "John Doe",
            GameDate = new DateTime(2025, 12, 7)
            // BlackPlayer and Round are null
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.Contains("[Date \"2025.12.07\"]", result);
        Assert.DoesNotContain("[Round", result);
        Assert.Contains("[White \"John Doe\"]", result);
        Assert.Contains("[Black \"?\"]", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithoutMetadata_ShouldUseDefaultValues()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, null);

        // Assert
        Assert.Contains("[Date \"????.??.??\"]", result);
        Assert.DoesNotContain("[Round", result);
        Assert.Contains("[White \"?\"]", result);
        Assert.Contains("[Black \"?\"]", result);
        Assert.Contains("[Result \"*\"]", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithEmptyPlayerNames_ShouldUseQuestionMark()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };
        var metadata = new PgnMetadata
        {
            WhitePlayer = "",
            BlackPlayer = "",
            GameDate = new DateTime(2025, 12, 7)
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.Contains("[White \"?\"]", result);
        Assert.Contains("[Black \"?\"]", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithWhitespacePlayerNames_ShouldUseQuestionMark()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };
        var metadata = new PgnMetadata
        {
            WhitePlayer = "   ",
            BlackPlayer = "   ",
            GameDate = new DateTime(2025, 12, 7)
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.Contains("[White \"?\"]", result);
        Assert.Contains("[Black \"?\"]", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithRound_ShouldIncludeRoundField()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };
        var metadata = new PgnMetadata
        {
            Round = "Final"
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.Contains("[Round \"Final\"]", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_WithEmptyRound_ShouldNotIncludeRoundField()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };
        var metadata = new PgnMetadata
        {
            Round = ""
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.DoesNotContain("[Round", result);
    }

    [Fact]
    public void GeneratePGNContentAsync_DateFormat_ShouldBeCorrect()
    {
        // Arrange
        var whiteMoves = new List<string> { "e4" };
        var blackMoves = new List<string> { "e5" };
        var metadata = new PgnMetadata
        {
            GameDate = new DateTime(2025, 1, 5) // Test single digit month and day
        };

        // Act
        var result = _service.GeneratePGNContentAsync(whiteMoves, blackMoves, metadata);

        // Assert
        Assert.Contains("[Date \"2025.01.05\"]", result);
    }
}

