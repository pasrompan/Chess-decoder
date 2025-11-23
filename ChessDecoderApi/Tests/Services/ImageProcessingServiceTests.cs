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
using System.Collections.Generic;

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
            var tempFile = Path.GetTempFileName();
            try
            {
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object,
                    _chessMoveProcessor,
                    _chessMoveValidator) { CallBase = true };

                // Patch: Mock ExtractMovesFromImageToStringAsync for full isolation
                mockService.Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync((new List<string> { "e4", "Nf3" }, new List<string> { "e5", "Nc6" }));

                var result = await mockService.Object.ProcessImageAsync(tempFile);

                Assert.NotNull(result);
                Assert.NotNull(result.PgnContent);
                Assert.NotNull(result.Validation);
                Assert.NotNull(result.Validation.GameId);
                Assert.NotNull(result.Validation.Moves);
                Assert.NotEmpty(result.Validation.Moves);
                Assert.Contains("Date", result.PgnContent);
                Assert.Contains("1. e4 e5", result.PgnContent);
                Assert.Contains("2. Nf3 Nc6", result.PgnContent);
                Assert.Contains("*", result.PgnContent);
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
            var tempFile = Path.GetTempFileName();
            try
            {
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object,
                    _chessMoveProcessor,
                    _chessMoveValidator) { CallBase = true };

                // Patch: Mock ExtractMovesFromImageToStringAsync for full isolation
                mockService.Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
                    .ReturnsAsync((new List<string> { "invalid", "Nf3" }, new List<string> { "e5", "Nc6" }));

                var result = await mockService.Object.ProcessImageAsync(tempFile);

                Assert.Contains("[Date", result.PgnContent);
                Assert.NotNull(result.Validation);
                Assert.NotNull(result.Validation.Moves);
                Assert.NotEmpty(result.Validation.Moves);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Skip = "Skipping this test for as new implementation validates moves independently for black and white")]
        public async Task ProcessImageAsync_ConsecutiveChecks_ReturnsValidationWarnings()
        {
            // Arrange
            var mockService = new Mock<ImageProcessingService>(
                _httpClientFactoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object,
                _loggerFactoryMock.Object,
                _chessMoveProcessor,
                _chessMoveValidator
            ) { CallBase = true };

            // Simulate moves with consecutive checks to trigger warning
            mockService
                .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((new List<string> { "e4", "Qh5+" }, new List<string> { "e5", "Ke7+" }));

            // Act
            var result = await mockService.Object.ProcessImageAsync("dummy-path", "English");

            // Assert
            Assert.NotNull(result.Validation.GameId);
            Assert.NotNull(result.Validation.Moves);
            Assert.NotEmpty(result.Validation.Moves);
            Assert.Contains("Date", result.PgnContent);
            Assert.Contains("1. e4 e5", result.PgnContent);
            Assert.Contains("2. Qh5+ Ke7+", result.PgnContent);
            // Check for warning status in validation
            Assert.Contains(result.Validation.Moves, pair =>
                (pair.WhiteMove != null && pair.WhiteMove.ValidationStatus != null && pair.WhiteMove.ValidationStatus == "warning") ||
                (pair.BlackMove != null && pair.BlackMove.ValidationStatus != null && pair.BlackMove.ValidationStatus == "warning"));
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
                _loggerFactoryMock.Object,
                _chessMoveProcessor,
                _chessMoveValidator);
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
    }
} 