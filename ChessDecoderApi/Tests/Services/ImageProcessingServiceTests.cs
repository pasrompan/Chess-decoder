using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ChessDecoderApi.Services;
using ChessDecoderApi.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using SixLabors.ImageSharp.PixelFormats;
using System.Linq;

namespace ChessDecoderApi.Tests.Services
{
    public class ImageProcessingServiceTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<ImageProcessingService>> _loggerMock;
        private readonly Mock<ILoggerFactory> _loggerFactoryMock;
        private readonly Mock<ILogger<ChessMoveProcessor>> _chessMoveProcessorLoggerMock;
        private readonly Mock<ILogger<ChessMoveValidator>> _chessMoveValidatorLoggerMock;
        private readonly IChessMoveProcessor _chessMoveProcessor;
        private readonly IChessMoveValidator _chessMoveValidator;
        private readonly ImageProcessingService _service;

        public ImageProcessingServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<ImageProcessingService>>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _chessMoveProcessorLoggerMock = new Mock<ILogger<ChessMoveProcessor>>();
            _chessMoveValidatorLoggerMock = new Mock<ILogger<ChessMoveValidator>>();
            _chessMoveProcessor = new ChessMoveProcessor(_chessMoveProcessorLoggerMock.Object);
            _chessMoveValidator = new ChessMoveValidator(_chessMoveValidatorLoggerMock.Object);

            // Setup logger factory to return our mock loggers
            _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s == typeof(ChessMoveProcessor).FullName)))
                .Returns(_chessMoveProcessorLoggerMock.Object);
            _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s == typeof(ChessMoveValidator).FullName)))
                .Returns(_chessMoveValidatorLoggerMock.Object);

            // Setup configuration to return a dummy API key
            _configurationMock.Setup(x => x["OPENAI_API_KEY"]).Returns("dummy-api-key");

            // Setup chess move processor mock
            /* _chessMoveProcessorMock.Setup(x => x.ProcessChessMovesAsync(It.IsAny<string>()))
                 .ReturnsAsync((string text) => 
                 {
                     // Parse the JSON array from the text and return the moves
                     if (text.Contains("e4") && text.Contains("e5"))
                         return new[] { "e4", "e5", "Nf3", "Nc6" };
                     if (text.Contains("ε4") && text.Contains("ε5"))
                         return new[] { "ε4", "ε5", "Ιf3", "Ιc6" };
                     if (text.Contains("invalid"))
                         return new[] { "invalid", "e5", "Nf3", "Nc6" };
                     if (text.Contains("Qh5+"))
                         return new[] { "e4", "e5", "Qh5+", "Ke7+" };
                     return new string[0];
                 });

             // Setup chess move validator mock
             _chessMoveValidatorMock.Setup(x => x.ValidateMoves(It.IsAny<string[]>()))
                 .Returns((string[] moves) => 
                 {
                     var result = new ChessDecoderApi.Services.ChessMoveValidationResult { IsValid = true };
                     for (int i = 0; i < moves.Length; i++)
                     {
                         var move = moves[i];
                         var validatedMove = new ChessDecoderApi.Services.ValidatedMove
                         {
                             MoveNumber = i + 1,
                             Notation = move,
                             NormalizedNotation = move,
                             ValidationStatus = "valid",
                             ValidationText = ""
                         };

                         // Add specific validation logic for test cases
                         if (move == "invalid")
                         {
                             validatedMove.ValidationStatus = "error";
                             validatedMove.ValidationText = "Invalid move";
                         }
                         else if (move.EndsWith("+"))
                         {
                             validatedMove.ValidationStatus = "warning";
                             validatedMove.ValidationText = "Consecutive checks";
                         }

                         result.Moves.Add(validatedMove);
                     }
                     return result;
                 });
 */
            // Create a partial mock of the service to override ExtractTextFromImageAsync
            _service = new ImageProcessingService(
                _httpClientFactoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object,
                _loggerFactoryMock.Object,
                _chessMoveProcessor,
                _chessMoveValidator);
              
            
        }

        [Fact]
        public async Task ProcessImageAsync_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonExistentPath = "nonexistent.jpg";

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _service.ProcessImageAsync(nonExistentPath));
        }

        [Fact]
        public async Task ProcessImageAsync_ValidEnglishMoves_ReturnsPGNContentAndValidation()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a small test image file
                using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(10, 10))
                using (var fs = File.OpenWrite(tempFile))
                {
                    image.Save(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                }

                // Mock ExtractTextFromImageAsync to return valid moves
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object,
                    _chessMoveProcessor,
                    _chessMoveValidator) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined English text without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "English"))
                    .ReturnsAsync("```json\n[\"e4\", \"e5\", \"Nf3\", \"Nc6\"]```");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile);

                // Assert
                Assert.NotNull(result);
                Assert.NotNull(result.PgnContent);
                Assert.NotNull(result.Validation);
                Assert.NotNull(result.Validation.GameId);
                Assert.NotEmpty(result.Validation.Moves);

                // Check PGN content
                Assert.Contains("[Event \"??\"]", result.PgnContent);
                Assert.Contains("1. e4 e5", result.PgnContent);
                Assert.Contains("2. Nf3 Nc6", result.PgnContent);
                Assert.Contains("*", result.PgnContent);

                // Check validation data
                Assert.Equal(2, result.Validation.Moves.Count); // 2 move pairs
                var firstPair = result.Validation.Moves[0];
                Assert.Equal(1, firstPair.MoveNumber);
                Assert.Equal("e4", firstPair.WhiteMove.Notation);
                Assert.Equal("e5", firstPair.BlackMove.Notation);
                Assert.Equal("valid", firstPair.WhiteMove.ValidationStatus);
                Assert.Equal("valid", firstPair.BlackMove.ValidationStatus);

                var secondPair = result.Validation.Moves[1];
                Assert.Equal(2, secondPair.MoveNumber);
                Assert.Equal("Nf3", secondPair.WhiteMove.Notation);
                Assert.Equal("Nc6", secondPair.BlackMove.Notation);
                Assert.Equal("valid", secondPair.WhiteMove.ValidationStatus);
                Assert.Equal("valid", secondPair.BlackMove.ValidationStatus);

                // Verify that ExtractTextFromImageAsync was called with our dummy image bytes
                mockService.Verify(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "English"), 
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task ProcessImageAsync_ValidGreekMoves_ReturnsPGNContentAndValidation()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a small test image file
                using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(10, 10))
                using (var fs = File.OpenWrite(tempFile))
                {
                    image.Save(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                }

                // Mock ExtractTextFromImageAsync to return Greek moves
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object,
                    _chessMoveProcessor,
                    _chessMoveValidator) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined Greek text without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })),
                    "Greek"))
                    .ReturnsAsync("```json\n[\"ε4\", \"ε5\", \"Ιf3\", \"Ιc6\"]```");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile, "Greek");

                // Assert
                Assert.NotNull(result);
                Assert.NotNull(result.PgnContent);
                Assert.NotNull(result.Validation);
                Assert.NotNull(result.Validation.GameId);
                Assert.NotEmpty(result.Validation.Moves);

                // Check PGN content
                Assert.Contains("[Event \"??\"]", result.PgnContent);
                Assert.Contains("1. e4 e5", result.PgnContent);
                Assert.Contains("2. Nf3 Nc6", result.PgnContent);
                Assert.Contains("*", result.PgnContent);

                // Check validation data
                Assert.Equal(2, result.Validation.Moves.Count); // 2 move pairs
                var firstPair = result.Validation.Moves[0];
                Assert.Equal(1, firstPair.MoveNumber);
                Assert.Equal("e4", firstPair.WhiteMove.Notation);
                Assert.Equal("e5", firstPair.BlackMove.Notation);
                Assert.Equal("valid", firstPair.WhiteMove.ValidationStatus);
                Assert.Equal("valid", firstPair.BlackMove.ValidationStatus);

                var secondPair = result.Validation.Moves[1];
                Assert.Equal(2, secondPair.MoveNumber);
                Assert.Equal("Nf3", secondPair.WhiteMove.Notation);
                Assert.Equal("Nc6", secondPair.BlackMove.Notation);
                Assert.Equal("valid", secondPair.WhiteMove.ValidationStatus);
                Assert.Equal("valid", secondPair.BlackMove.ValidationStatus);

                // Verify that ExtractTextFromImageAsync was called with our dummy image bytes
                mockService.Verify(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "Greek"), 
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task ProcessImageAsync_InvalidMoves_ReturnsValidationErrors()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a small test image file
                using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(10, 10))
                using (var fs = File.OpenWrite(tempFile))
                {
                    image.Save(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                }

                // Mock ExtractTextFromImageAsync to return invalid moves
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object,
                    _chessMoveProcessor,
                    _chessMoveValidator) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined invalid text without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "English"))
                    .ReturnsAsync("```json\n[\"invalid\", \"e5\", \"Nf3\", \"Nc6\"]```");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile);

                // Assert
                Assert.Contains("[Date", result);
                // Verify that validation errors were logged
                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Error,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Move validation error")),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);

                // Verify that ExtractTextFromImageAsync was called with our dummy image bytes
                mockService.Verify(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "English"), 
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public async Task ProcessImageAsync_ConsecutiveChecks_ReturnsValidationWarnings()
        {
            // Arrange
            var tempFile = Path.GetTempFileName();
            try
            {
                // Create a small test image file
                using (var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(10, 10))
                using (var fs = File.OpenWrite(tempFile))
                {
                    image.Save(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
                }
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header

                // Mock ExtractMovesFromImageToStringAsync to return moves with consecutive checks
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object,
                    _chessMoveProcessor,
                    _chessMoveValidator) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined moves with consecutive checks without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "English"))
                    .ReturnsAsync("```json\n[\"e4\", \"e5\", \"Qh5+\", \"Ke7+\"]```");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile);

                // Assert
                Assert.Contains("[Date", result);
                // Verify that validation warning was logged
                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Move validation warning")),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtLeastOnce);

                // Verify that ExtractTextFromImageAsync was called with our dummy image bytes
                mockService.Verify(x => x.ExtractTextFromImageAsync(
                    It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 0x00, 0x01, 0x02 })), 
                    "English"), 
                    Times.Once);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void SplitImageIntoColumns_ReturnsExpectedNumberOfBoundaries()
        {
            // Arrange: Create a synthetic image with 4 vertical columns
            int width = 400;
            int height = 100;
            int columns = 4;
            using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
            var colors = new[] {
                SixLabors.ImageSharp.Color.Black,
                SixLabors.ImageSharp.Color.White,
                SixLabors.ImageSharp.Color.Gray,
                SixLabors.ImageSharp.Color.Red
            };
            for (int i = 0; i < columns; i++)
            {
                int xStart = i * width / columns;
                int xEnd = (i + 1) * width / columns;
                for (int x = xStart; x < xEnd; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        image[x, y] = colors[i].ToPixel<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                    }
                }
            }
            string tempPath = Path.GetTempFileName() + ".jpg";
            using (var fs = File.OpenWrite(tempPath))
            {
                image.Save(fs, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
            }
            var service = new ChessDecoderApi.Services.ImageProcessingService(
                _httpClientFactoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object,
                _loggerFactoryMock.Object);
            try
            {
                // Act
                var result = service.SplitImageIntoColumns(tempPath, columns);
                // Assert
                Assert.Equal(columns + 1, result.Count); // boundaries = columns + 1
                Assert.True(result.SequenceEqual(result.OrderBy(x => x)), "Boundaries should be sorted");
                Assert.Equal(0, result.First());
                Assert.Equal(width, result.Last());
                foreach (var b in result)
                {
                    Assert.InRange(b, 0, width);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void SplitImageIntoColumns_FindsBoundariesInGame2Picture()
        {
            // Arrange
            string imagePath = Path.Combine("data", "EvaluationExamples", "Game2", "Game2rot.png");
            var service = new ChessDecoderApi.Services.ImageProcessingService(
                _httpClientFactoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object,
                _loggerFactoryMock.Object);

            // Act
            var result = service.SplitImageIntoColumns(imagePath, 6);

            // Assert
            Assert.Equal(7, result.Count); // 6 columns = 7 boundaries
            Assert.True(result.SequenceEqual(result.OrderBy(x => x)), "Boundaries should be sorted");
            Assert.Equal(0, result.First());
            // We can't know the exact width, but boundaries should be increasing
            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(result[i] > result[i - 1], $"Boundary {i} should be greater than {i - 1}");
            }
        }
    }
} 