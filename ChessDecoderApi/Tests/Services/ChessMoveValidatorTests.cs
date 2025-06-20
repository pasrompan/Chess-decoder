using System;
using System.Linq;
using ChessDecoderApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services
{
    public class ChessMoveValidatorTests
    {
        private readonly ChessMoveValidator _validator;
        private readonly Mock<ILogger<ChessMoveValidator>> _loggerMock;

        public ChessMoveValidatorTests()
        {
            _loggerMock = new Mock<ILogger<ChessMoveValidator>>();
            _validator = new ChessMoveValidator(_loggerMock.Object);
        }

        [Fact]
        public void ValidateMoves_ValidMoves_ReturnsValidResult()
        {
            // Arrange
            var moves = new[] { "e4", "e5", "Nf3", "Nc6", "Bb5", "a6" };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid);
            Assert.Equal(moves.Length, result.Moves.Count);
            Assert.All(result.Moves, move => 
            {
                Assert.Equal("valid", move.ValidationStatus);
                Assert.Empty(move.ValidationText);
                Assert.Equal(move.Notation, move.NormalizedNotation);
            });
        }

        [Fact]
        public void ValidateMoves_InvalidSyntax_ReturnsErrors()
        {
            // Arrange
            var moves = new[] { "e4", "invalid", "Nf3" };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.False(result.IsValid);
            var invalidMove = result.Moves.First(m => m.Notation == "invalid");
            Assert.Equal("error", invalidMove.ValidationStatus);
            Assert.Contains("Invalid move syntax", invalidMove.ValidationText);
        }

        [Fact]
        public void ValidateMoves_InvalidPiece_ReturnsErrors()
        {
            // Arrange
            var moves = new[] { "e4", "Xe5" }; // X is not a valid piece

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.False(result.IsValid);
            var invalidMove = result.Moves.First(m => m.Notation == "Xe5");
            Assert.Equal("error", invalidMove.ValidationStatus);
            Assert.Contains("Invalid move syntax 'Xe5'", invalidMove.ValidationText);
        }

        [Theory]
        [InlineData("0-0", "O-O")]    // Kingside with zeros
        [InlineData("O-O", "O-O")]    // Kingside with O's
        [InlineData("o-o", "O-O")]    // Kingside with o's
        [InlineData("0-O", "O-O")]    // Mixed kingside
        [InlineData("O-0", "O-O")]    // Mixed kingside
        [InlineData("o-0", "O-O")]    // Mixed kingside
        [InlineData("0-o", "O-O")]    // Mixed kingside
        [InlineData("O-o", "O-O")]    // Mixed kingside
        [InlineData("o-O", "O-O")]    // Mixed kingside
        [InlineData("0-0+", "O-O+")]  // Kingside with check
        [InlineData("O-O#", "O-O#")]  // Kingside with checkmate
        public void ValidateMoves_KingsideCastling_NormalizesCorrectly(string input, string expected)
        {
            // Arrange
            var moves = new[] { "e4", "e5", input };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid, $"Castling notation '{input}' should be valid");
            var castlingMove = result.Moves.First(m => m.Notation == input);
            Assert.Equal("valid", castlingMove.ValidationStatus);
            Assert.Equal(expected, castlingMove.NormalizedNotation);
        }

        [Theory]
        [InlineData("0-0-0", "O-O-O")]    // Queenside with zeros
        [InlineData("O-O-O", "O-O-O")]    // Queenside with O's
        [InlineData("o-o-o", "O-O-O")]    // Queenside with o's
        [InlineData("0-O-0", "O-O-O")]    // Mixed queenside
        [InlineData("O-0-O", "O-O-O")]    // Mixed queenside
        [InlineData("o-0-o", "O-O-O")]    // Mixed queenside
        [InlineData("0-o-0", "O-O-O")]    // Mixed queenside
        [InlineData("O-o-O", "O-O-O")]    // Mixed queenside
        [InlineData("o-O-o", "O-O-O")]    // Mixed queenside
        [InlineData("0-0-O", "O-O-O")]    // Mixed queenside
        [InlineData("O-O-0", "O-O-O")]    // Mixed queenside
        [InlineData("o-o-0", "O-O-O")]    // Mixed queenside
        [InlineData("0-O-o", "O-O-O")]    // Mixed queenside
        [InlineData("O-0-o", "O-O-O")]    // Mixed queenside
        [InlineData("o-0-O", "O-O-O")]    // Mixed queenside
        [InlineData("0-0-0+", "O-O-O+")]  // Queenside with check
        [InlineData("O-O-O#", "O-O-O#")]  // Queenside with checkmate
        public void ValidateMoves_QueensideCastling_NormalizesCorrectly(string input, string expected)
        {
            // Arrange
            var moves = new[] { "e4", "e5", input };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid, $"Castling notation '{input}' should be valid");
            var castlingMove = result.Moves.First(m => m.Notation == input);
            Assert.Equal("valid", castlingMove.ValidationStatus);
            Assert.Equal(expected, castlingMove.NormalizedNotation);
        }

        [Fact]
        public void ValidateMoves_MixedCastlingNotations_NormalizesCorrectly()
        {
            // Arrange
            var moves = new[] { "0-0", "O-O-O", "o-o", "0-0-0", "O-O" };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid);
            Assert.All(result.Moves, move => Assert.Equal("valid", move.ValidationStatus));
            Assert.Collection(result.Moves,
                move => Assert.Equal("O-O", move.NormalizedNotation),
                move => Assert.Equal("O-O-O", move.NormalizedNotation),
                move => Assert.Equal("O-O", move.NormalizedNotation),
                move => Assert.Equal("O-O-O", move.NormalizedNotation),
                move => Assert.Equal("O-O", move.NormalizedNotation)
            );
        }

        [Fact]
        public void ValidateMoves_InvalidPromotion_ProvidesSuggestion()
        {
            // Arrange
            var moves = new[] { "e4", "e8=X" }; // Invalid promotion piece

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.False(result.IsValid);
            var invalidMove = result.Moves.First(m => m.Notation == "e8=X");
            Assert.Equal("error", invalidMove.ValidationStatus);
            Assert.Contains("Invalid promotion piece", invalidMove.ValidationText);
        }

        [Fact]
        public void ValidateMoves_ConsecutiveChecks_AddsWarning()
        {
            // Arrange
            var moves = new[] { "e4", "Qh5+", "Ke7+" };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid);
            var checkMoves = result.Moves.Where(m => m.Notation.EndsWith("+")).ToList();
            Assert.All(checkMoves, move => Assert.Equal("warning", move.ValidationStatus));
            Assert.All(checkMoves, move => Assert.Contains("Consecutive checks detected", move.ValidationText));
        }

        [Fact]
        public void ValidateMoves_ValidPromotion_ReturnsValidResult()
        {
            // Arrange
            var moves = new[] { "e4", "e5", "e8=Q" };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid);
            var promotionMove = result.Moves.First(m => m.Notation == "e8=Q");
            Assert.Equal("valid", promotionMove.ValidationStatus);
            Assert.Empty(promotionMove.ValidationText);
        }

        [Fact]
        public void ValidateMoves_Checkmate_ReturnsValidResult()
        {
            // Arrange
            var moves = new[] { "e4", "e5", "Qh5#", "Nc6" };

            // Act
            var result = _validator.ValidateMoves(moves);

            // Assert
            Assert.True(result.IsValid);
            var checkmateMove = result.Moves.First(m => m.Notation == "Qh5#");
            Assert.Equal("valid", checkmateMove.ValidationStatus);
            Assert.Empty(checkmateMove.ValidationText);
        }

        [Fact]
        public void ValidateMoves_NullInput_ReturnsError()
        {
            // Act
            var result = _validator.ValidateMoves(null);

            // Assert
            Assert.False(result.IsValid);
            var errorMove = result.Moves.First();
            Assert.Equal(0, errorMove.MoveNumber);
            Assert.Equal("error", errorMove.ValidationStatus);
            Assert.Contains("No moves provided", errorMove.ValidationText);
        }

        [Fact]
        public void ValidateMoves_EmptyArray_ReturnsError()
        {
            // Act
            var result = _validator.ValidateMoves(Array.Empty<string>());

            // Assert
            Assert.False(result.IsValid);
            var errorMove = result.Moves.First();
            Assert.Equal(0, errorMove.MoveNumber);
            Assert.Equal("error", errorMove.ValidationStatus);
            Assert.Contains("No moves provided", errorMove.ValidationText);
        }
    }
} 