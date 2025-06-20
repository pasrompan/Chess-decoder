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
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header

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
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header

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
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header

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
                File.WriteAllBytes(tempFile, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Minimal JPEG header

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
    }
} 