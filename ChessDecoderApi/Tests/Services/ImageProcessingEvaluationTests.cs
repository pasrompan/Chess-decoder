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

        /// <summary>
        /// Example of how to run a controlled evaluation with mock data
        /// </summary>
        [Fact]
        public async Task RunControlledEvaluation_Example()
        {
            var mockImageService = new Mock<IImageProcessingService>();

            // Mock the tuple return: whiteMoves, blackMoves
            mockImageService
                .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((new List<string> { "e4", "Nf3", "Bb5" }, new List<string> { "e5", "Nc6" }));

            mockImageService
                .Setup(x => x.GeneratePGNContentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
                .Returns("[Event \"Test Game\"]\n\n1. e4 e5 2. Nf3 Nc6 3. Bb5 *");

            var evaluationService = new ImageProcessingEvaluationService(
                mockImageService.Object, 
                _loggerMock.Object, 
                useRealApi: true);

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

        /// <summary>
        /// Tests evaluation with realistic chess game data showing partial accuracy
        /// </summary>
        [Fact]
        public async Task EvaluateAsync_WithRealisticGameData_ShouldReturnReasonableMetrics()
        {
            // Arrange
            var mockImageService = new Mock<IImageProcessingService>();

            var groundTruthMoves = new string[]
            {
                "e4", "e5", "Nf3", "Nc6", "Bb5", "Bb4", "c3", "Ba5", "O-O", "Nf6",
                "Re1", "O-O", "Na3", "d5", "exd5", "Qxd5", "b4", "e4", "Bxc6", "bxc6",
                "Nd4", "Bg4", "Qb3", "Rab8", "Qxd5", "cxd5", "bxa5", "c5", "Nc6", "Rbc8",
                "Ne7+", "Kh8", "Nxc8", "Rxc8", "Nc2", "Rd8", "Ne3", "Bh5", "Ba3", "d4",
                "Nf5", "Rd5", "Ne7", "Rd7", "Bxc5", "d3", "Rab1", "Rd8", "Rb7", "g6",
                "Bd4", "h6", "Bxf6+", "Kh7", "Nf5", "Rd5", "Rxf7+", "Kg8", "Nxh6#"
            };

            var extractedMoves = new string[]
            {
                "e4", "c5", "Nf3", "Nc6", "Bb5", "Bb7", "O-O", "Ba5", "c3", "Nf6",
                "Re1", "O-O", "Na3", "d5", "exd5", "Qxd5", "b4", "e4", "Bxc6", "bxc6",
                "Nd4", "Bg4", "Qa4", "Rfd8", "Bxg4", "Bxc3", "Nxc6", "Qxd2", "Ne7+", "Kh8",
                "Nxc8", "Raxc8", "Nc6", "Rd3", "Ne5", "Bf3", "Bf3", "d4", "Nf5", "Rd5",
                "Nxe7", "Rf7", "Rxe4", "gxf3", "Ra4", "Rg8", "Bf4", "g5", "Bxf6", "g4",
                "Bf4", "Bxf4", "Bxf4", "g3", "Rxf7", "fxg2", "Nh6+", "Rg8"
            };

            // Split extractedMoves into white and black lists
            var whiteMoves = new List<string>();
            var blackMoves = new List<string>();
            for (int i = 0; i < extractedMoves.Length; i++)
            {
                if (i % 2 == 0) whiteMoves.Add(extractedMoves[i]);
                else blackMoves.Add(extractedMoves[i]);
            }

            mockImageService
                .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((whiteMoves, blackMoves));

            mockImageService
                .Setup(x => x.GeneratePGNContentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
                .Returns("[Event \"Test Game\"]\n\n1. e4 c5 2. Nf3 Nc6 *");

            var evaluationService = new ImageProcessingEvaluationService(
                mockImageService.Object, 
                _loggerMock.Object, 
                useRealApi: true);

            var tempGroundTruthPath = Path.GetTempFileName();
            var tempImagePath = Path.GetTempFileName();

            try
            {
                var pgnContent = "1. e4 e5 2. Nf3 Nc6 3. Bb5 Bb4 4. c3 Ba5 5. O-O Nf6 " +
                               "6. Re1 O-O 7. Na3 d5 8. exd5 Qxd5 9. b4 e4 10. Bxc6 bxc6 " +
                               "11. Nd4 Bg4 12. Qb3 Rab8 13. Qxd5 cxd5 14. bxa5 c5 15. Nc6 Rbc8 " +
                               "16. Ne7+ Kh8 17. Nxc8 Rxc8 18. Nc2 Rd8 19. Ne3 Bh5 20. Ba3 d4 " +
                               "21. Nf5 Rd5 22. Ne7 Rd7 23. Bxc5 d3 24. Rab1 Rd8 25. Rb7 g6 " +
                               "26. Bd4 h6 27. Bxf6+ Kh7 28. Nf5 Rd5 29. Rxf7+ Kg8 30. Nxh6# *";

                await File.WriteAllTextAsync(tempGroundTruthPath, pgnContent);
                File.WriteAllBytes(tempImagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // Dummy image

                var result = await evaluationService.EvaluateAsync(tempImagePath, tempGroundTruthPath);

                Assert.True(result.IsSuccessful);
                Assert.Equal(59, result.GroundTruthMoves.Count);
                Assert.Equal(58, result.ExtractedMoves.Count);

                Assert.True(result.NormalizedScore >= 0.25 && result.NormalizedScore <= 0.45, 
                    $"Normalized score {result.NormalizedScore:F3} should be between 0.25 and 0.45");
                Assert.True(result.ExactMatchScore >= 0.25 && result.ExactMatchScore <= 0.35, 
                    $"Exact match score {result.ExactMatchScore:F3} should be between 0.25 and 0.35");
                Assert.True(result.PositionalAccuracy >= 0.25 && result.PositionalAccuracy <= 0.35, 
                    $"Positional accuracy {result.PositionalAccuracy:F3} should be between 0.25 and 0.35");
                Assert.True(result.LevenshteinDistance >= 30 && result.LevenshteinDistance <= 40, 
                    $"Levenshtein distance {result.LevenshteinDistance} should be between 30 and 40");
                Assert.True(result.LongestCommonSubsequence >= 20 && result.LongestCommonSubsequence <= 30, 
                    $"LCS {result.LongestCommonSubsequence} should be between 20 and 30");

                mockImageService.Verify(x => x.ExtractMovesFromImageToStringAsync(tempImagePath, "English"), Times.Once);
                mockImageService.Verify(x => x.GeneratePGNContentAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()), Times.Once);

                _output.WriteLine($"=== Realistic Game Evaluation Results ===");
                _output.WriteLine($"Normalized Score: {result.NormalizedScore:F3}");
                _output.WriteLine($"Exact Match Score: {result.ExactMatchScore:F3}");
                _output.WriteLine($"Positional Accuracy: {result.PositionalAccuracy:F3}");
                _output.WriteLine($"Levenshtein Distance: {result.LevenshteinDistance}");
                _output.WriteLine($"Longest Common Subsequence: {result.LongestCommonSubsequence}");
                _output.WriteLine($"Ground Truth Moves: {result.GroundTruthMoves.Count}");
                _output.WriteLine($"Extracted Moves: {result.ExtractedMoves.Count}");
            }
            finally
            {
                if (File.Exists(tempGroundTruthPath))
                    File.Delete(tempGroundTruthPath);
                if (File.Exists(tempImagePath))
                    File.Delete(tempImagePath);
            }
        }
    }
} 