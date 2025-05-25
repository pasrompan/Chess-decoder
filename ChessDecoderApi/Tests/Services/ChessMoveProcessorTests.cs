using System;
using System.Threading.Tasks;
using System.IO;
using ChessDecoderApi.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ChessDecoderApi.Tests.Services
{
    public class ChessMoveProcessorTests
    {
        private readonly ChessMoveProcessor _processor;
        private readonly Mock<ILogger<ChessMoveProcessor>> _loggerMock;

        public ChessMoveProcessorTests()
        {
            _loggerMock = new Mock<ILogger<ChessMoveProcessor>>();
            _processor = new ChessMoveProcessor(_loggerMock.Object);
        }

        [Fact]
        public async Task ProcessChessMovesAsync_ValidInput_ReturnsCorrectMoves()
        {
            // Arrange
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var filePath = Path.Combine(basePath, "data", "ExampleResponse.txt");
            var input = await File.ReadAllTextAsync(filePath);

            // Act
            var result = await _processor.ProcessChessMovesAsync(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.Length); // Total number of moves in the example
            Assert.Equal("e4", result[0]);
            Assert.Equal("c5", result[1]);
            Assert.Equal("Nf3", result[2]);
            Assert.Equal("Nc6", result[3]);
        }

        [Fact]
        public async Task ProcessChessMovesAsync_InvalidJson_ThrowsArgumentException()
        {
            // Arrange
            var input = "invalid json";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _processor.ProcessChessMovesAsync(input));
        }

        [Fact]
        public async Task ProcessChessMovesAsync_NullResponse_ThrowsArgumentException()
        {
            // Arrange
            var input = @"{ ""response"": null }";

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _processor.ProcessChessMovesAsync(input));
        }
    }
} 