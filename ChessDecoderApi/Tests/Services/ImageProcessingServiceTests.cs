using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ChessDecoderApi.Services;
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
        private readonly ImageProcessingService _service;

        public ImageProcessingServiceTests()
        {
            _httpClientFactoryMock = new Mock<IHttpClientFactory>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<ImageProcessingService>>();
            _loggerFactoryMock = new Mock<ILoggerFactory>();
            _chessMoveProcessorLoggerMock = new Mock<ILogger<ChessMoveProcessor>>();
            _chessMoveValidatorLoggerMock = new Mock<ILogger<ChessMoveValidator>>();

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
                _loggerFactoryMock.Object);
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
        public async Task ProcessImageAsync_ValidEnglishMoves_ReturnsPGNContent()
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
                    _loggerFactoryMock.Object) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined moves without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(It.IsAny<byte[]>(), "English"))
                    .ReturnsAsync(@"json
[""e4"", ""e5"", ""Nf3"", ""Nc6""]");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile);

                // Assert
                Assert.Contains("[Event \"??\"]", result);
                Assert.Contains("1. e4 e5", result);
                Assert.Contains("2. Nf3 Nc6", result);
                Assert.Contains("*", result);

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
        public async Task ProcessImageAsync_ValidGreekMoves_ReturnsPGNContent()
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
                    _loggerFactoryMock.Object) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined moves without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(It.IsAny<byte[]>(), "Greek"))
                    .ReturnsAsync(@"json
[""ε4"", ""ε5"", ""Ιf3"", ""Ιc6""]");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile, "Greek");

                // Assert
                Assert.Contains("[Event \"??\"]", result);
                Assert.Contains("1. e4 e5", result);
                Assert.Contains("2. Nf3 Nc6", result);
                Assert.Contains("*", result);

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
        public async Task ProcessImageAsync_InvalidMoves_LogsValidationErrors()
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
                    _loggerFactoryMock.Object) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined moves without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(It.IsAny<byte[]>(), "English"))
                    .ReturnsAsync(@"json
[""invalid"", ""e5"", ""Nf3"", ""Nc6""]");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile);

                // Assert
                Assert.Contains("[Event \"??\"]", result);
                // Verify that validation errors were logged
                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Error,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Move validation error")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
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
        public async Task ProcessImageAsync_ConsecutiveChecks_LogsValidationWarning()
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

                // Mock ExtractTextFromImageAsync to return moves with consecutive checks
                var mockService = new Mock<ImageProcessingService>(
                    _httpClientFactoryMock.Object,
                    _configurationMock.Object,
                    _loggerMock.Object,
                    _loggerFactoryMock.Object) { CallBase = true };

                // Mock the image loading part
                mockService.Protected()
                    .Setup<Task<byte[]>>("LoadAndProcessImageAsync", ItExpr.Is<string>(s => s == tempFile))
                    .ReturnsAsync(new byte[] { 0x00, 0x01, 0x02 }); // Dummy image bytes

                // Mock ExtractTextFromImageAsync to return predefined moves without making API calls
                mockService.Setup(x => x.ExtractTextFromImageAsync(It.IsAny<byte[]>(), "English"))
                    .ReturnsAsync(@"json
[""e4"", ""e5"", ""Qh5+"", ""Ke7+""]");

                // Verify that no HTTP client is created (no API calls)
                _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);

                // Act
                var result = await mockService.Object.ProcessImageAsync(tempFile);

                // Assert
                Assert.Contains("[Event \"??\"]", result);
                // Verify that validation warning was logged
                _loggerMock.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Move validation warning")),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>()),
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