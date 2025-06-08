using System.Collections.Generic;
using System.Linq;
using ChessDecoderApi.Tests.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services
{
    public class ExtractMovesFromPgnTests
    {
        private readonly ImageProcessingEvaluationService _service;

        public ExtractMovesFromPgnTests()
        {
            var mockImageService = new Mock<ChessDecoderApi.Services.IImageProcessingService>();
            var mockLogger = new Mock<ILogger<ImageProcessingEvaluationService>>();
            _service = new ImageProcessingEvaluationService(mockImageService.Object, mockLogger.Object);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithFullPgnFormat_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = @"[Event ""Test Tournament""]
[Site ""Test City""]
[Date ""2024.01.01""]
[Round ""1""]
[White ""Player1""]
[Black ""Player2""]
[Result ""*""]

1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 4. Ba4 Nf6 *";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(8, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
            Assert.Equal("a6", moves[5]);
            Assert.Equal("Ba4", moves[6]);
            Assert.Equal("Nf6", moves[7]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithoutHeaders_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bb5 *";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(5, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithWhiteWinResult_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Qh5 Nc6 3. Bc4 g6 4. Qxf7# 1-0";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(7, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Qh5", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bc4", moves[4]);
            Assert.Equal("g6", moves[5]);
            Assert.Equal("Qxf7#", moves[6]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithBlackWinResult_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. f3 e5 2. g4 Qh4# 0-1";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(4, moves.Count);
            Assert.Equal("f3", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("g4", moves[2]);
            Assert.Equal("Qh4#", moves[3]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithDrawResult_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 1/2-1/2";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(6, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
            Assert.Equal("a6", moves[5]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithIncompleteGame_ShouldReturnAvailableMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bb5";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(5, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithCastling_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bc4 Bc5 4. O-O O-O 5. d3 d6";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(10, moves.Count);
            Assert.Contains("O-O", moves);
            Assert.Equal("O-O", moves[6]); // White castling
            Assert.Equal("O-O", moves[7]); // Black castling
        }

        [Fact]
        public void ExtractMovesFromPgn_WithQueensideCastling_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. d4 d5 2. Nc3 Nc6 3. Bd2 Bd7 4. Qc1 Qc8 5. O-O-O O-O-O";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(10, moves.Count);
            Assert.Contains("O-O-O", moves);
            Assert.Equal("O-O-O", moves[8]); // White queenside castling
            Assert.Equal("O-O-O", moves[9]); // Black queenside castling
        }

        [Fact]
        public void ExtractMovesFromPgn_WithCaptures_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Nf3 d6 3. Nxe5 dxe5 4. Qh5 Nf6 5. Qxe5+";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(9, moves.Count);
            Assert.Equal("Nxe5", moves[4]);
            Assert.Equal("dxe5", moves[5]);
            Assert.Equal("Qxe5+", moves[8]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithPawnPromotion_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. a4 a5 3. b4 axb4 4. a5 b3 5. a6 bxa2 6. axb7 a1=Q 7. bxa8=Q";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(13, moves.Count);
            Assert.Contains("a1=Q", moves);
            Assert.Contains("bxa8=Q", moves);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithChecksAndCheckmate_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. e4 e5 2. Bc4 Nc6 3. Qh5 Nf6 4. Qxf7# *";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(7, moves.Count);
            Assert.Equal("Qxf7#", moves[6]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithExtraWhitespace_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = @"
            
1.   e4    e5   2.  Nf3   Nc6   3.   Bb5   *
            
            ";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(5, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithMultipleLines_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = @"1. e4 e5 
2. Nf3 Nc6 
3. Bb5 a6 
4. Ba4 Nf6 
*";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(8, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
            Assert.Equal("a6", moves[5]);
            Assert.Equal("Ba4", moves[6]);
            Assert.Equal("Nf6", moves[7]);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithEmptyContent_ShouldReturnEmptyList()
        {
            // Arrange
            var pgnContent = "";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Empty(moves);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithOnlyHeaders_ShouldReturnEmptyList()
        {
            // Arrange
            var pgnContent = @"[Event ""Test""]
[Site ""Test""]
[Date ""2024.01.01""]
[Round ""1""]
[White ""Player1""]
[Black ""Player2""]
[Result ""*""]";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Empty(moves);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithOnlyResultMarker_ShouldReturnEmptyList()
        {
            // Arrange
            var pgnContent = "*";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Empty(moves);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithRealGameExample_ShouldReturnCorrectMoves()
        {
            // Arrange - Using the actual Game1.txt format from the test data
            var pgnContent = @"1. e4 e5 
2. Nf3 Nc6 
3. Bb5 Bb4 
4. c3 Ba5 
5. O-O Nf6 
6. Re1 O-O 
7. Na3 d5 
8. exd5 Qxd5 
*";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(16, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
            Assert.Equal("Bb4", moves[5]);
            Assert.Equal("c3", moves[6]);
            Assert.Equal("Ba5", moves[7]);
            Assert.Equal("O-O", moves[8]);
            Assert.Equal("Nf6", moves[9]);
            Assert.Equal("Re1", moves[10]);
            Assert.Equal("O-O", moves[11]);
            Assert.Equal("Na3", moves[12]);
            Assert.Equal("d5", moves[13]);
            Assert.Equal("exd5", moves[14]);
            Assert.Equal("Qxd5", moves[15]);
        }

        [Theory]
        [InlineData("1. e4 e5 *", 2)]
        [InlineData("1. e4 e5 2. Nf3 *", 3)]
        [InlineData("1. e4 e5 2. Nf3 Nc6 *", 4)]
        [InlineData("1. e4 *", 1)]
        public void ExtractMovesFromPgn_WithVariousMoveCounts_ShouldReturnCorrectCount(string pgnContent, int expectedCount)
        {
            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(expectedCount, moves.Count);
        }

        [Fact]
        public void ExtractMovesFromPgn_WithAmbiguousNotation_ShouldReturnCorrectMoves()
        {
            // Arrange
            var pgnContent = "1. Nf3 Nf6 2. Ng1 Ng8 3. Nf3 Nf6 4. Nh4 Nh5";

            // Act
            var moves = _service.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(8, moves.Count);
            Assert.Equal("Nf3", moves[0]);
            Assert.Equal("Nf6", moves[1]);
            Assert.Equal("Ng1", moves[2]);
            Assert.Equal("Ng8", moves[3]);
            Assert.Equal("Nf3", moves[4]);
            Assert.Equal("Nf6", moves[5]);
            Assert.Equal("Nh4", moves[6]);
            Assert.Equal("Nh5", moves[7]);
        }
    }
} 