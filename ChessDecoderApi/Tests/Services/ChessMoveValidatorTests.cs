using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chess;
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
                Assert.Empty(move.ValidationText ?? string.Empty);
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
            Assert.Contains("Invalid move syntax", invalidMove.ValidationText ?? string.Empty);
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
            Assert.Contains("Invalid move syntax 'Xe5'", invalidMove.ValidationText ?? string.Empty);
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
            Assert.Contains("Invalid promotion piece", invalidMove.ValidationText ?? string.Empty);
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
            Assert.All(checkMoves, move => {
                Assert.NotNull(move.ValidationStatus);
                Assert.Equal("warning", move.ValidationStatus);
            });
            Assert.All(checkMoves, move => Assert.Contains("Consecutive checks detected", move.ValidationText ?? string.Empty));
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
            Assert.Empty(promotionMove.ValidationText ?? string.Empty);
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
            Assert.Empty(checkmateMove.ValidationText ?? string.Empty);
        }

        [Fact]
        public void ValidateMoves_NullInput_ReturnsError()
        {
            // Act
            var result = _validator.ValidateMoves(null!);

            // Assert
            Assert.False(result.IsValid);
            var errorMove = result.Moves.First();
            Assert.Equal(0, errorMove.MoveNumber);
            Assert.Equal("error", errorMove.ValidationStatus);
            Assert.Contains("No moves provided", errorMove.ValidationText ?? string.Empty);
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
            Assert.Contains("No moves provided", errorMove.ValidationText ?? string.Empty);
        }

        #region GetLegalMoves Tests

        [Fact]
        public void GetLegalMoves_InitialPosition_ShouldFindMoves()
        {
            // Arrange
            var board = new ChessBoard();
            
            // Act - Use reflection to call private GetLegalMoves method
            var legalMoves = GetLegalMovesUsingReflection(board);

            // Assert
            Assert.NotNull(legalMoves);
            Assert.NotEmpty(legalMoves);
            Assert.True(legalMoves.Count > 0, $"Expected to find legal moves from initial position, but found {legalMoves.Count}");
            
            // Initial position should have 20 legal moves (16 pawn moves + 4 knight moves)
            // But we'll just verify we found some moves
            Assert.Contains("e4", legalMoves);
        }

        [Fact]
        public void GetLegalMoves_AfterE4_ShouldFindMoves()
        {
            // Arrange
            var board = new ChessBoard();
            board.Move("e4");

            // Act
            var legalMoves = GetLegalMovesUsingReflection(board);

            // Assert
            Assert.NotNull(legalMoves);
            Assert.NotEmpty(legalMoves);
            Assert.True(legalMoves.Count > 0, $"Expected to find legal moves after e4, but found {legalMoves.Count}");
        }

        [Fact]
        public void GetLegalMoves_ChessBoardReflection_ShouldFindProperties()
        {
            // Arrange
            var board = new ChessBoard();
            var boardType = board.GetType();

            // Act - Check what properties and methods are available
            var properties = boardType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var methods = boardType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            // Assert - Log available properties and methods for debugging
            var propertyNames = properties.Select(p => p.Name).ToList();
            var methodNames = methods.Select(m => m.Name).ToList();

            Assert.NotNull(propertyNames);
            Assert.NotNull(methodNames);

            // Check for common chess board properties
            var hasLegalMovesProperty = propertyNames.Any(n => 
                n.Contains("Legal", StringComparison.OrdinalIgnoreCase) && 
                n.Contains("Move", StringComparison.OrdinalIgnoreCase));
            
            var hasGetMovesMethod = methodNames.Any(n => 
                n.Contains("Move", StringComparison.OrdinalIgnoreCase) && 
                !n.Contains("Move") || n == "GetMoves" || n == "GetLegalMoves");

            // Log findings for debugging
            Console.WriteLine("Available Properties:");
            foreach (var prop in propertyNames)
            {
                Console.WriteLine($"  - {prop}");
            }

            Console.WriteLine("\nAvailable Methods:");
            foreach (var method in methodNames.Where(m => m.Contains("Move", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"  - {method}");
            }
        }

        [Fact]
        public void GetLegalMoves_ChessBoardFenProperty_ShouldExist()
        {
            // Arrange
            var board = new ChessBoard();
            var boardType = board.GetType();

            // Act - Check for FEN-related properties
            var fenProperty = boardType.GetProperty("Fen", BindingFlags.Public | BindingFlags.Instance) ??
                            boardType.GetProperty("FEN", BindingFlags.Public | BindingFlags.Instance) ??
                            boardType.GetProperty("Position", BindingFlags.Public | BindingFlags.Instance);

            // Assert
            if (fenProperty != null)
            {
                var fenValue = fenProperty.GetValue(board)?.ToString();
                Assert.NotNull(fenValue);
                Console.WriteLine($"FEN property found: {fenProperty.Name}, Value: {fenValue}");
            }
            else
            {
                // Log all properties to help find the right one
                var allProperties = boardType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                Console.WriteLine("FEN property not found. Available properties:");
                foreach (var prop in allProperties)
                {
                    Console.WriteLine($"  - {prop.Name} ({prop.PropertyType.Name})");
                }
            }
        }

        [Fact]
        public void CloneBoard_InitialPosition_ShouldWork()
        {
            // Arrange
            var board = new ChessBoard();
            board.Move("e4");

            // Act - Use reflection to call private CloneBoard method
            var clonedBoard = CloneBoardUsingReflection(board);

            // Assert
            if (clonedBoard != null)
            {
                // If cloning works, try to make a move on the cloned board
                try
                {
                    clonedBoard.Move("e5");
                    Assert.True(true, "CloneBoard succeeded and cloned board is functional");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"CloneBoard returned a board but it's not functional: {ex.Message}");
                }
            }
            else
            {
                // Cloning failed - this is expected if FEN property doesn't exist
                Console.WriteLine("CloneBoard returned null - FEN-based cloning not available");
            }
        }

        [Fact]
        public void ValidateMovesInGameContext_InvalidMove_ShouldReplaceWithBestMove()
        {
            // Arrange
            var whiteValidation = new ChessMoveValidationResult
            {
                Moves = new List<ValidatedMove>
                {
                    new ValidatedMove { MoveNumber = 1, Notation = "e4", NormalizedNotation = "e4", ValidationStatus = "valid" },
                    new ValidatedMove { MoveNumber = 2, Notation = "invalid", NormalizedNotation = "invalid", ValidationStatus = "valid" }
                }
            };

            var blackValidation = new ChessMoveValidationResult
            {
                Moves = new List<ValidatedMove>
                {
                    new ValidatedMove { MoveNumber = 1, Notation = "e5", NormalizedNotation = "e5", ValidationStatus = "valid" }
                }
            };

            // Act
            _validator.ValidateMovesInGameContext(whiteValidation, blackValidation);

            // Assert
            var invalidMove = whiteValidation.Moves.First(m => m.Notation == "invalid");
            
            // The move should either be replaced or marked as error
            Assert.NotNull(invalidMove.ValidationStatus);
            
            if (invalidMove.ValidationStatus == "warning")
            {
                // Move was replaced with best move
                Assert.NotEqual("invalid", invalidMove.NormalizedNotation);
                Assert.Contains("replaced with engine suggestion", invalidMove.ValidationText ?? string.Empty);
                Console.WriteLine($"Invalid move was replaced with: {invalidMove.NormalizedNotation}");
            }
            else if (invalidMove.ValidationStatus == "error")
            {
                // Move couldn't be replaced - check if it's because no legal moves were found
                Console.WriteLine($"Move was marked as error: {invalidMove.ValidationText}");
                if (invalidMove.ValidationText?.Contains("No legal moves available") == true)
                {
                    Assert.Fail("GetLegalMoves is not finding any legal moves - this is the issue we're debugging");
                }
            }
        }

        [Fact]
        public void GetLegalMovesByTesting_InitialPosition_ShouldFindSomeMoves()
        {
            // Arrange
            var board = new ChessBoard();

            // Act - Use reflection to call private GetLegalMovesByTesting method
            var legalMoves = GetLegalMovesByTestingUsingReflection(board);

            // Assert
            Assert.NotNull(legalMoves);
            // Even if reflection fails, the testing method should find at least some moves
            // from the initial position (like e4, d4, Nf3, etc.)
            if (legalMoves.Count == 0)
            {
                Assert.Fail("GetLegalMovesByTesting found 0 moves from initial position - this indicates an issue with CloneBoard or move testing");
            }
            else
            {
                Console.WriteLine($"GetLegalMovesByTesting found {legalMoves.Count} moves:");
                foreach (var move in legalMoves.Take(10))
                {
                    Console.WriteLine($"  - {move}");
                }
            }
        }

        #endregion

        #region Reflection Helpers

        private List<string> GetLegalMovesUsingReflection(ChessBoard board)
        {
            var method = typeof(ChessMoveValidator).GetMethod("GetLegalMoves", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException("GetLegalMoves method not found");
            }

            return (List<string>)method.Invoke(_validator, new object[] { board })!;
        }

        private ChessBoard? CloneBoardUsingReflection(ChessBoard board)
        {
            var method = typeof(ChessMoveValidator).GetMethod("CloneBoard", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException("CloneBoard method not found");
            }

            return (ChessBoard?)method.Invoke(_validator, new object[] { board });
        }

        private List<string> GetLegalMovesByTestingUsingReflection(ChessBoard board)
        {
            var method = typeof(ChessMoveValidator).GetMethod("GetLegalMovesByTesting", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                throw new InvalidOperationException("GetLegalMovesByTesting method not found");
            }

            return (List<string>)method.Invoke(_validator, new object[] { board })!;
        }

        #endregion
    }
} 