using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ChessDecoderApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ChessDecoderApi.Tests.Services
{
    /// <summary>
    /// Tests for the ImageProcessingEvaluationService.
    /// These tests are marked with [Fact(Skip = "...")] by default to prevent accidental API calls.
    /// To run real evaluations, change Skip to null and ensure OPENAI_API_KEY is set.
    /// </summary>
    public class ImageProcessingEvaluationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<ILogger<ImageProcessingEvaluationService>> _loggerMock;
        
        public ImageProcessingEvaluationTests(ITestOutputHelper output)
        {
            _output = output;
            _loggerMock = new Mock<ILogger<ImageProcessingEvaluationService>>();
        }

        [Fact(Skip = "Requires real OpenAI API key and test images - enable manually")]
        public async Task EvaluateAsync_WithRealApiEnabled_ShouldProcessImageAndComputeScore()
        {
            // Arrange - Create real service instances
            var httpClientFactory = new Mock<IHttpClientFactory>().Object;
            var configuration = new Mock<IConfiguration>();
            configuration.Setup(x => x["OPENAI_API_KEY"]).Returns(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            
            var imageServiceLogger = new Mock<ILogger<ImageProcessingService>>();
            var loggerFactory = new Mock<ILoggerFactory>();
            var imageProcessingService = new ImageProcessingService(
                httpClientFactory, 
                configuration.Object, 
                imageServiceLogger.Object, 
                loggerFactory.Object);

            var evaluationService = new ImageProcessingEvaluationService(
                imageProcessingService, 
                _loggerMock.Object, 
                useRealApi: true); // Enable real API usage

            // Act - This would require real test images and ground truth files
            var imagePath = "Tests/data/ExampleGamePics/Game1.jpg"; // You would need to provide this
            var groundTruthPath = "Tests/data/GroundTruth/Game1.txt";

            if (File.Exists(imagePath) && File.Exists(groundTruthPath))
            {
                var result = await evaluationService.EvaluateAsync(imagePath, groundTruthPath);

                // Assert
                Assert.True(result.IsSuccessful);
                Assert.True(result.NormalizedScore >= 0.0);
                Assert.True(result.NormalizedScore <= 1.0);
                Assert.True(result.ExactMatchScore >= 0.0);
                Assert.True(result.ExactMatchScore <= 1.0);
                
                // Print results to test output
                result.PrintSummary();
                _output.WriteLine($"Evaluation completed with normalized score: {result.NormalizedScore:F3}");
            }
            else
            {
                _output.WriteLine("Test images not found. Skipping actual evaluation.");
            }
        }

        [Fact(Skip = "Requires real OpenAI API key and test images - enable manually")]
        public async Task EvaluateMultipleAsync_WithMultipleTestCases_ShouldReturnAggregateResults()
        {
            // Arrange
            var httpClientFactory = new Mock<IHttpClientFactory>().Object;
            var configuration = new Mock<IConfiguration>();
            configuration.Setup(x => x["OPENAI_API_KEY"]).Returns(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            
            var imageServiceLogger = new Mock<ILogger<ImageProcessingService>>();
            var loggerFactory = new Mock<ILoggerFactory>();
            var imageProcessingService = new ImageProcessingService(
                httpClientFactory, 
                configuration.Object, 
                imageServiceLogger.Object, 
                loggerFactory.Object);

            var evaluationService = new ImageProcessingEvaluationService(
                imageProcessingService, 
                _loggerMock.Object, 
                useRealApi: true);

            var testCases = new List<TestCase>
            {
                new TestCase 
                { 
                    ImagePath = "Tests/data/test-chess-image1.jpg", 
                    GroundTruthPath = "Tests/data/GroundTruth/Game1.txt",
                    Language = "English"
                },
                new TestCase 
                { 
                    ImagePath = "Tests/data/test-chess-image2.jpg", 
                    GroundTruthPath = "Tests/data/GroundTruth/Game2.txt",
                    Language = "English"
                }
            };

            // Act
            var aggregateResult = await evaluationService.EvaluateMultipleAsync(testCases);

            // Assert
            Assert.Equal(testCases.Count, aggregateResult.TotalTestCases);
            Assert.True(aggregateResult.AverageNormalizedScore >= 0.0);
            Assert.True(aggregateResult.AverageNormalizedScore <= 1.0);

            // Print aggregate results
            aggregateResult.PrintSummary();
            _output.WriteLine($"Aggregate evaluation completed for {aggregateResult.TotalTestCases} test cases");
        }

        [Fact]
        public async Task EvaluateAsync_WithRealApiDisabled_ShouldThrowException()
        {
            // Arrange
            var imageProcessingService = new Mock<IImageProcessingService>();
            var evaluationService = new ImageProcessingEvaluationService(
                imageProcessingService.Object, 
                _loggerMock.Object, 
                useRealApi: false); // Disable real API usage

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => 
                evaluationService.EvaluateAsync("test.jpg", "test.txt"));
        }

        [Fact]
        public void ExtractMovesFromPgn_WithValidPgnContent_ShouldReturnCorrectMoves()
        {
            // Arrange
            var imageProcessingService = new Mock<IImageProcessingService>();
            var evaluationService = new ImageProcessingEvaluationService(
                imageProcessingService.Object, 
                _loggerMock.Object, 
                useRealApi: false);

            var pgnContent = @"[Event ""Test Game""]
[Site ""Test Site""]
[Date ""2024.01.01""]
[Round ""1""]
[White ""Player1""]
[Black ""Player2""]
[Result ""*""]

1. e4 e5 2. Nf3 Nc6 3. Bb5 a6 *";

            // Act
            var moves = evaluationService.ExtractMovesFromPgn(pgnContent);

            // Assert
            Assert.Equal(6, moves.Count);
            Assert.Equal("e4", moves[0]);
            Assert.Equal("e5", moves[1]);
            Assert.Equal("Nf3", moves[2]);
            Assert.Equal("Nc6", moves[3]);
            Assert.Equal("Bb5", moves[4]);
            Assert.Equal("a6", moves[5]);
        }

        /// <summary>
        /// Example of how to run a controlled evaluation with mock data
        /// </summary>
        [Fact]
        public async Task RunControlledEvaluation_Example()
        {
            // This demonstrates how you would set up a controlled evaluation
            // without actually calling the OpenAI API

            var mockImageService = new Mock<IImageProcessingService>();

            // Mock the ProcessImageAsync to return a known PGN result

            mockImageService
                .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new string[] { "e4", "e5", "Nf3", "Nc6", "Bb5" }); // Perfect match with ground truth

            var evaluationService = new ImageProcessingEvaluationService(
                mockImageService.Object, 
                _loggerMock.Object, 
                useRealApi: true); // We can enable this since we're mocking the actual API calls

            // Create a temporary ground truth file
            var tempGroundTruthPath = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tempGroundTruthPath, @"1. e4 e5 
2. Nf3 Nc6 
3. Bb5 a6 
*");

                var tempImagePath = Path.GetTempFileName();
                File.WriteAllBytes(tempImagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Dummy image

                try
                {
                    // This would work because the image service is mocked
                    var result = await evaluationService.EvaluateAsync(tempImagePath, tempGroundTruthPath);
                    
                    Assert.True(result.IsSuccessful);
                    Assert.True(result.NormalizedScore >= 0.0);
                    
                    _output.WriteLine($"Mock evaluation score: {result.NormalizedScore:F3}");
                    _output.WriteLine($"Ground truth moves: {result.GroundTruthMoves.Count}");
                    _output.WriteLine($"Extracted moves: {result.ExtractedMoves.Count}");
                }
                finally
                {
                    File.Delete(tempImagePath);
                }
            }
            finally
            {
                File.Delete(tempGroundTruthPath);
            }
        }
    }
} 