using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ChessDecoderApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ChessDecoderApi.Services
{
    public class ImageProcessingEvaluationService
    {
        private readonly IImageProcessingService _imageProcessingService;
        private readonly IChessMoveValidator? _chessMoveValidator;
        private readonly ILogger<ImageProcessingEvaluationService> _logger;
        private readonly bool _useRealApi;

        public ImageProcessingEvaluationService(
            IImageProcessingService imageProcessingService,
            ILogger<ImageProcessingEvaluationService> logger,
            bool useRealApi = false)
        {
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _useRealApi = useRealApi;
            _chessMoveValidator = null; // Will be null if not provided - normalized moves won't be calculated
        }

        public ImageProcessingEvaluationService(
            IImageProcessingService imageProcessingService,
            IChessMoveValidator chessMoveValidator,
            ILogger<ImageProcessingEvaluationService> logger,
            bool useRealApi = false)
        {
            _imageProcessingService = imageProcessingService ?? throw new ArgumentNullException(nameof(imageProcessingService));
            _chessMoveValidator = chessMoveValidator ?? throw new ArgumentNullException(nameof(chessMoveValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _useRealApi = useRealApi;
        }

        /// <summary>
        /// Evaluates the ProcessImageAsync function against ground truth data
        /// </summary>
        /// <param name="imagePath">Path to the test image</param>
        /// <param name="groundTruthPath">Path to the ground truth PGN file</param>
        /// <param name="language">Language for chess notation (default: English)</param>
        /// <param name="autoCrop">Whether to automatically crop the image to table boundaries before processing (default: false)</param>
        /// <returns>Evaluation result with normalized score (0 = perfect)</returns>
        public async Task<EvaluationResult> EvaluateAsync(string imagePath, string groundTruthPath, string language = "English", bool autoCrop = false)
        {
            if (!_useRealApi)
            {
                throw new InvalidOperationException("Real API usage is disabled. Set useRealApi flag to true to enable evaluation.");
            }

            var result = new EvaluationResult
            {
                ImagePath = imagePath,
                GroundTruthPath = groundTruthPath,
                Language = language
            };

            // Detect language from the image
            string detectedLanguage = "English";
            try
            {
                detectedLanguage = await _imageProcessingService.DetectLanguageAsync(imagePath);
                result.DetectedLanguage = detectedLanguage;
                _logger.LogInformation("Detected language: {DetectedLanguage} (Expected: {ExpectedLanguage})", detectedLanguage, language);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to detect language, defaulting to English");
                result.DetectedLanguage = "English";
            }

            // Declare croppedImagePath outside try block so it's accessible in catch block for cleanup
            string? croppedImagePath = null;

            try
            {
                // Load ground truth moves
                var groundTruthMoves = await LoadGroundTruthMovesAsync(groundTruthPath);
                result.GroundTruthMoves = groundTruthMoves;

                _logger.LogInformation("Loaded {Count} ground truth moves from {Path}", 
                    groundTruthMoves.Count, groundTruthPath);

                // Handle autoCrop if enabled
                string imagePathForProcessing = imagePath;
                
                if (autoCrop)
                {
                    _logger.LogInformation("Auto-crop enabled, finding table boundaries and cropping image");
                    
                    using var originalImage = Image.Load<Rgba32>(imagePath);
                    var tableBoundaries = _imageProcessingService.FindTableBoundaries(originalImage);
                    
                    var croppedImageBytes = await _imageProcessingService.CropImageAsync(
                        imagePath, 
                        tableBoundaries.X, 
                        tableBoundaries.Y, 
                        tableBoundaries.Width, 
                        tableBoundaries.Height);

                    var extension = Path.HasExtension(imagePath) ? Path.GetExtension(imagePath) : ".jpg";
                    var croppedFileName = $"{Guid.NewGuid()}_cropped{extension}";
                    croppedImagePath = Path.Combine(Path.GetTempPath(), croppedFileName);
                    await File.WriteAllBytesAsync(croppedImagePath, croppedImageBytes);
                    
                    imagePathForProcessing = croppedImagePath;
                }

                // Extract moves directly from the image (language is auto-detected)
                var startTime = DateTime.UtcNow;
                var (whiteMoves, blackMoves) = await _imageProcessingService.ExtractMovesFromImageToStringAsync(imagePathForProcessing);
                var extractedMoves = new List<string>();
                int maxMoves = Math.Max(whiteMoves.Count, blackMoves.Count);
                for (int i = 0; i < maxMoves; i++)
                {
                    if (i < whiteMoves.Count) extractedMoves.Add(whiteMoves[i]);
                    if (i < blackMoves.Count) extractedMoves.Add(blackMoves[i]);
                }
                result.ExtractedMoves = extractedMoves;
                _logger.LogInformation("Extracted {Count} moves directly from image", extractedMoves.Count);
                result.GeneratedPgn = _imageProcessingService.GeneratePGNContentAsync(whiteMoves, blackMoves);

                // Compute various distance metrics for extracted moves
                result.ExactMatchScore = ComputeExactMatchScore(groundTruthMoves, extractedMoves);
                result.LevenshteinDistance = ComputeLevenshteinDistance(groundTruthMoves, extractedMoves);
                result.PositionalAccuracy = ComputePositionalAccuracy(groundTruthMoves, extractedMoves);
                result.LongestCommonSubsequence = ComputeLongestCommonSubsequence(groundTruthMoves, extractedMoves);

                // Compute normalized score (0 = perfect, 1 = worst)
                result.NormalizedScore = ComputeNormalizedScore(result);

                // Get normalized moves if validator is available
                if (_chessMoveValidator != null)
                {
                    // Validate moves to get normalized versions
                    var whiteValidation = _chessMoveValidator.ValidateMoves(whiteMoves.ToArray());
                    var blackValidation = _chessMoveValidator.ValidateMoves(blackMoves.ToArray());
                    _chessMoveValidator.ValidateMovesInGameContext(whiteValidation, blackValidation);

                    // Build normalized moves list
                    var normalizedMoves = new List<string>();
                    int maxNormalizedMoves = Math.Max(whiteValidation.Moves.Count, blackValidation.Moves.Count);
                    for (int i = 0; i < maxNormalizedMoves; i++)
                    {
                        if (i < whiteValidation.Moves.Count && !string.IsNullOrWhiteSpace(whiteValidation.Moves[i].NormalizedNotation))
                        {
                            normalizedMoves.Add(whiteValidation.Moves[i].NormalizedNotation!);
                        }
                        if (i < blackValidation.Moves.Count && !string.IsNullOrWhiteSpace(blackValidation.Moves[i].NormalizedNotation))
                        {
                            normalizedMoves.Add(blackValidation.Moves[i].NormalizedNotation!);
                        }
                    }
                    result.NormalizedMoves = normalizedMoves;
                    _logger.LogInformation("Normalized {Count} moves after validation", normalizedMoves.Count);

                    // Compute metrics for normalized moves
                    result.NormalizedExactMatchScore = ComputeExactMatchScore(groundTruthMoves, normalizedMoves);
                    result.NormalizedLevenshteinDistance = ComputeLevenshteinDistance(groundTruthMoves, normalizedMoves);
                    result.NormalizedPositionalAccuracy = ComputePositionalAccuracy(groundTruthMoves, normalizedMoves);
                    result.NormalizedLongestCommonSubsequence = ComputeLongestCommonSubsequence(groundTruthMoves, normalizedMoves);

                    // Compute normalized score for normalized moves
                    result.NormalizedNormalizedScore = ComputeNormalizedScoreForMoves(
                        groundTruthMoves, 
                        normalizedMoves, 
                        result.NormalizedExactMatchScore,
                        result.NormalizedLevenshteinDistance,
                        result.NormalizedPositionalAccuracy,
                        result.NormalizedLongestCommonSubsequence);
                }

                result.IsSuccessful = true;

                _logger.LogInformation("Evaluation completed. Normalized Score: {Score:F3}", result.NormalizedScore);
                
                // Clean up cropped image if it was created
                if (croppedImagePath != null && File.Exists(croppedImagePath))
                {
                    try
                    {
                        File.Delete(croppedImagePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cropped image file: {Path}", croppedImagePath);
                    }
                }
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error during evaluation of image {ImagePath}", imagePath);
                
                // Clean up cropped image if it was created
                if (croppedImagePath != null && File.Exists(croppedImagePath))
                {
                    try
                    {
                        File.Delete(croppedImagePath);
                    }
                    catch (Exception cleanupEx)
                    {
                        _logger.LogWarning(cleanupEx, "Failed to delete cropped image file during error cleanup: {Path}", croppedImagePath);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Evaluates multiple test cases and returns aggregate results
        /// </summary>
        /// <param name="testCases">List of test cases with image and ground truth pairs</param>
        /// <returns>Aggregate evaluation results</returns>
        public async Task<AggregateEvaluationResult> EvaluateMultipleAsync(IEnumerable<TestCase> testCases)
        {
            var results = new List<EvaluationResult>();
            var aggregateResult = new AggregateEvaluationResult();

            foreach (var testCase in testCases)
            {
                _logger.LogInformation("Evaluating test case: {ImagePath}", testCase.ImagePath);
                var result = await EvaluateAsync(testCase.ImagePath, testCase.GroundTruthPath, testCase.Language);
                results.Add(result);
            }

            aggregateResult.IndividualResults = results;
            aggregateResult.TotalTestCases = results.Count;
            aggregateResult.SuccessfulTestCases = results.Count(r => r.IsSuccessful);
            
            if (aggregateResult.SuccessfulTestCases > 0)
            {
                var successfulResults = results.Where(r => r.IsSuccessful).ToList();
                aggregateResult.AverageNormalizedScore = successfulResults.Average(r => r.NormalizedScore);
                aggregateResult.AverageExactMatchScore = successfulResults.Average(r => r.ExactMatchScore);
                aggregateResult.AveragePositionalAccuracy = successfulResults.Average(r => r.PositionalAccuracy);
                aggregateResult.AverageProcessingTime = TimeSpan.FromMilliseconds(
                    successfulResults.Average(r => r.ProcessingTime.TotalMilliseconds));
            }

            return aggregateResult;
        }

        /// <summary>
        /// Evaluates dual-page images against ground truth data
        /// </summary>
        /// <param name="page1Path">Path to the first page image</param>
        /// <param name="page2Path">Path to the second page image</param>
        /// <param name="groundTruthPath">Path to the ground truth PGN file</param>
        /// <param name="language">Language for chess notation (default: English)</param>
        /// <param name="autoCrop">Whether to automatically crop images to table boundaries before processing (default: false)</param>
        /// <returns>Evaluation result with normalized score (0 = perfect)</returns>
        public async Task<EvaluationResult> EvaluateDualAsync(string page1Path, string page2Path, string groundTruthPath, string language = "English", bool autoCrop = false)
        {
            if (!_useRealApi)
            {
                throw new InvalidOperationException("Real API usage is disabled. Set useRealApi flag to true to enable evaluation.");
            }

            var result = new EvaluationResult
            {
                ImagePath = $"{page1Path} + {page2Path}",
                GroundTruthPath = groundTruthPath,
                Language = language
            };

            string? croppedPage1Path = null;
            string? croppedPage2Path = null;

            try
            {
                var groundTruthMoves = await LoadGroundTruthMovesAsync(groundTruthPath);
                result.GroundTruthMoves = groundTruthMoves;

                _logger.LogInformation("Loaded {Count} ground truth moves from {Path}", groundTruthMoves.Count, groundTruthPath);

                string page1ForProcessing = page1Path;
                string page2ForProcessing = page2Path;

                if (autoCrop)
                {
                    _logger.LogInformation("Auto-crop enabled for dual pages");
                    
                    using var page1Image = Image.Load<Rgba32>(page1Path);
                    var page1Boundaries = _imageProcessingService.FindTableBoundaries(page1Image);
                    var croppedPage1Bytes = await _imageProcessingService.CropImageAsync(
                        page1Path, page1Boundaries.X, page1Boundaries.Y, page1Boundaries.Width, page1Boundaries.Height);
                    var ext1 = Path.GetExtension(page1Path);
                    croppedPage1Path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_page1_cropped{ext1}");
                    await File.WriteAllBytesAsync(croppedPage1Path, croppedPage1Bytes);
                    page1ForProcessing = croppedPage1Path;

                    using var page2Image = Image.Load<Rgba32>(page2Path);
                    var page2Boundaries = _imageProcessingService.FindTableBoundaries(page2Image);
                    var croppedPage2Bytes = await _imageProcessingService.CropImageAsync(
                        page2Path, page2Boundaries.X, page2Boundaries.Y, page2Boundaries.Width, page2Boundaries.Height);
                    var ext2 = Path.GetExtension(page2Path);
                    croppedPage2Path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_page2_cropped{ext2}");
                    await File.WriteAllBytesAsync(croppedPage2Path, croppedPage2Bytes);
                    page2ForProcessing = croppedPage2Path;
                }

                var startTime = DateTime.UtcNow;

                // Extract moves from page 1
                var (whiteMoves1, blackMoves1) = await _imageProcessingService.ExtractMovesFromImageToStringAsync(page1ForProcessing);
                
                // Extract moves from page 2
                var (whiteMoves2, blackMoves2) = await _imageProcessingService.ExtractMovesFromImageToStringAsync(page2ForProcessing);

                // Combine moves from both pages
                var whiteMoves = whiteMoves1.Concat(whiteMoves2).ToList();
                var blackMoves = blackMoves1.Concat(blackMoves2).ToList();

                var extractedMoves = new List<string>();
                int maxMoves = Math.Max(whiteMoves.Count, blackMoves.Count);
                for (int i = 0; i < maxMoves; i++)
                {
                    if (i < whiteMoves.Count) extractedMoves.Add(whiteMoves[i]);
                    if (i < blackMoves.Count) extractedMoves.Add(blackMoves[i]);
                }

                result.ExtractedMoves = extractedMoves;
                result.ProcessingTime = DateTime.UtcNow - startTime;
                
                _logger.LogInformation("Extracted {Count} moves from dual pages (Page1: {P1W}W/{P1B}B, Page2: {P2W}W/{P2B}B)", 
                    extractedMoves.Count, whiteMoves1.Count, blackMoves1.Count, whiteMoves2.Count, blackMoves2.Count);
                
                result.GeneratedPgn = _imageProcessingService.GeneratePGNContentAsync(whiteMoves, blackMoves);

                // Compute metrics
                result.ExactMatchScore = ComputeExactMatchScore(groundTruthMoves, extractedMoves);
                result.LevenshteinDistance = ComputeLevenshteinDistance(groundTruthMoves, extractedMoves);
                result.PositionalAccuracy = ComputePositionalAccuracy(groundTruthMoves, extractedMoves);
                result.LongestCommonSubsequence = ComputeLongestCommonSubsequence(groundTruthMoves, extractedMoves);
                result.NormalizedScore = ComputeNormalizedScore(result);

                // Get normalized moves if validator is available
                if (_chessMoveValidator != null)
                {
                    var whiteValidation = _chessMoveValidator.ValidateMoves(whiteMoves.ToArray());
                    var blackValidation = _chessMoveValidator.ValidateMoves(blackMoves.ToArray());
                    _chessMoveValidator.ValidateMovesInGameContext(whiteValidation, blackValidation);

                    var normalizedMoves = new List<string>();
                    int maxNormalizedMoves = Math.Max(whiteValidation.Moves.Count, blackValidation.Moves.Count);
                    for (int i = 0; i < maxNormalizedMoves; i++)
                    {
                        if (i < whiteValidation.Moves.Count && !string.IsNullOrWhiteSpace(whiteValidation.Moves[i].NormalizedNotation))
                        {
                            normalizedMoves.Add(whiteValidation.Moves[i].NormalizedNotation!);
                        }
                        if (i < blackValidation.Moves.Count && !string.IsNullOrWhiteSpace(blackValidation.Moves[i].NormalizedNotation))
                        {
                            normalizedMoves.Add(blackValidation.Moves[i].NormalizedNotation!);
                        }
                    }
                    result.NormalizedMoves = normalizedMoves;

                    result.NormalizedExactMatchScore = ComputeExactMatchScore(groundTruthMoves, normalizedMoves);
                    result.NormalizedLevenshteinDistance = ComputeLevenshteinDistance(groundTruthMoves, normalizedMoves);
                    result.NormalizedPositionalAccuracy = ComputePositionalAccuracy(groundTruthMoves, normalizedMoves);
                    result.NormalizedLongestCommonSubsequence = ComputeLongestCommonSubsequence(groundTruthMoves, normalizedMoves);
                    result.NormalizedNormalizedScore = ComputeNormalizedScoreForMoves(
                        groundTruthMoves, normalizedMoves,
                        result.NormalizedExactMatchScore, result.NormalizedLevenshteinDistance,
                        result.NormalizedPositionalAccuracy, result.NormalizedLongestCommonSubsequence);
                }

                result.IsSuccessful = true;
                _logger.LogInformation("Dual evaluation completed. Normalized Score: {Score:F3}", result.NormalizedScore);
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error during dual page evaluation");
            }
            finally
            {
                if (croppedPage1Path != null && File.Exists(croppedPage1Path))
                {
                    try { File.Delete(croppedPage1Path); } catch { }
                }
                if (croppedPage2Path != null && File.Exists(croppedPage2Path))
                {
                    try { File.Delete(croppedPage2Path); } catch { }
                }
            }

            return result;
        }

        private async Task<List<string>> LoadGroundTruthMovesAsync(string pgnPath)
        {
            if (!File.Exists(pgnPath))
            {
                throw new FileNotFoundException($"Ground truth file not found: {pgnPath}");
            }

            var content = await File.ReadAllTextAsync(pgnPath);
            return ExtractMovesFromPgn(content);
        }

        public List<string> ExtractMovesFromPgn(string pgnContent)
        {
            var moves = new List<string>();
            
            // Remove PGN headers and metadata
            var lines = pgnContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var movesSection = string.Join(" ", lines.Where(line => !line.StartsWith("[") && !string.IsNullOrWhiteSpace(line)));
            
            // Remove result markers and extra whitespace
            movesSection = movesSection.Replace("*", "").Replace("1-0", "").Replace("0-1", "").Replace("1/2-1/2", "");
            
            // Extract moves using regex pattern
            var movePattern = @"\d+\.\s*([^\s]+)(?:\s+([^\s]+))?";
            var matches = Regex.Matches(movesSection, movePattern);
            
            foreach (Match match in matches)
            {
                // Add white move
                if (match.Groups[1].Success)
                {
                    var whiteMove = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(whiteMove) && whiteMove != "*")
                    {
                        moves.Add(whiteMove);
                    }
                }
                
                // Add black move if present
                if (match.Groups[2].Success)
                {
                    var blackMove = match.Groups[2].Value.Trim();
                    if (!string.IsNullOrEmpty(blackMove) && blackMove != "*")
                    {
                        moves.Add(blackMove);
                    }
                }
            }
            
            return moves;
        }

        private double ComputeExactMatchScore(List<string> groundTruth, List<string> extracted)
        {
            if (groundTruth.Count == 0) return extracted.Count == 0 ? 1.0 : 0.0;
            
            int exactMatches = 0;
            int maxLength = Math.Max(groundTruth.Count, extracted.Count);
            
            for (int i = 0; i < maxLength; i++)
            {
                var gtMove = i < groundTruth.Count ? groundTruth[i] : null;
                var exMove = i < extracted.Count ? extracted[i] : null;
                
                if (gtMove == exMove)
                {
                    exactMatches++;
                }
            }
            
            return (double)exactMatches / maxLength;
        }

        private int ComputeLevenshteinDistance(List<string> groundTruth, List<string> extracted)
        {
            int[,] dp = new int[groundTruth.Count + 1, extracted.Count + 1];
            
            for (int i = 0; i <= groundTruth.Count; i++)
                dp[i, 0] = i;
            
            for (int j = 0; j <= extracted.Count; j++)
                dp[0, j] = j;
            
            for (int i = 1; i <= groundTruth.Count; i++)
            {
                for (int j = 1; j <= extracted.Count; j++)
                {
                    if (groundTruth[i - 1] == extracted[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1];
                    }
                    else
                    {
                        dp[i, j] = 1 + Math.Min(
                            Math.Min(dp[i - 1, j], dp[i, j - 1]), 
                            dp[i - 1, j - 1]);
                    }
                }
            }
            
            return dp[groundTruth.Count, extracted.Count];
        }

        private double ComputePositionalAccuracy(List<string> groundTruth, List<string> extracted)
        {
            if (groundTruth.Count == 0) return extracted.Count == 0 ? 1.0 : 0.0;
            
            int correctPositions = 0;
            int minLength = Math.Min(groundTruth.Count, extracted.Count);
            
            for (int i = 0; i < minLength; i++)
            {
                if (groundTruth[i] == extracted[i])
                {
                    correctPositions++;
                }
            }
            
            return (double)correctPositions / groundTruth.Count;
        }

        private int ComputeLongestCommonSubsequence(List<string> groundTruth, List<string> extracted)
        {
            int[,] dp = new int[groundTruth.Count + 1, extracted.Count + 1];
            
            for (int i = 1; i <= groundTruth.Count; i++)
            {
                for (int j = 1; j <= extracted.Count; j++)
                {
                    if (groundTruth[i - 1] == extracted[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                    }
                }
            }
            
            return dp[groundTruth.Count, extracted.Count];
        }

        private double ComputeNormalizedScore(EvaluationResult result)
        {
            // Weighted combination of different metrics
            // Higher score is better (1 = perfect, 0 = worst)
            
            const double exactMatchWeight = 0.4;
            const double positionalWeight = 0.3;
            const double levenshteinWeight = 0.2;
            const double lcsWeight = 0.1;
            
            // Normalize each metric to 0-1 range where 0 is worst
            double exactMatchComponent = 1.0 - result.ExactMatchScore;
            double positionalComponent = 1.0 - result.PositionalAccuracy;
            
            // Normalize Levenshtein distance
            int maxPossibleDistance = Math.Max(result.GroundTruthMoves.Count, result.ExtractedMoves.Count);
            double levenshteinComponent = maxPossibleDistance > 0 ? (double)result.LevenshteinDistance / maxPossibleDistance : 0.0;
            
            // Normalize LCS (higher LCS is better, so we invert it)
            int maxPossibleLcs = Math.Min(result.GroundTruthMoves.Count, result.ExtractedMoves.Count);
            double lcsComponent = maxPossibleLcs > 0 ? 1.0 - ((double)result.LongestCommonSubsequence / maxPossibleLcs) : 1.0;
            
            double rawScore = exactMatchWeight * exactMatchComponent +
                             positionalWeight * positionalComponent +
                             levenshteinWeight * levenshteinComponent +
                             lcsWeight * lcsComponent;
            
            // Invert the score so that 1 = perfect, 0 = worst
            return 1.0 - rawScore;
        }

        private double ComputeNormalizedScoreForMoves(
            List<string> groundTruth, 
            List<string> moves, 
            double exactMatchScore,
            int levenshteinDistance,
            double positionalAccuracy,
            int longestCommonSubsequence)
        {
            // Weighted combination of different metrics
            // Higher score is better (1 = perfect, 0 = worst)
            
            const double exactMatchWeight = 0.4;
            const double positionalWeight = 0.3;
            const double levenshteinWeight = 0.2;
            const double lcsWeight = 0.1;
            
            // Normalize each metric to 0-1 range where 0 is worst
            double exactMatchComponent = 1.0 - exactMatchScore;
            double positionalComponent = 1.0 - positionalAccuracy;
            
            // Normalize Levenshtein distance
            int maxPossibleDistance = Math.Max(groundTruth.Count, moves.Count);
            double levenshteinComponent = maxPossibleDistance > 0 ? (double)levenshteinDistance / maxPossibleDistance : 0.0;
            
            // Normalize LCS (higher LCS is better, so we invert it)
            int maxPossibleLcs = Math.Min(groundTruth.Count, moves.Count);
            double lcsComponent = maxPossibleLcs > 0 ? 1.0 - ((double)longestCommonSubsequence / maxPossibleLcs) : 1.0;
            
            double rawScore = exactMatchWeight * exactMatchComponent +
                             positionalWeight * positionalComponent +
                             levenshteinWeight * levenshteinComponent +
                             lcsWeight * lcsComponent;
            
            // Invert the score so that 1 = perfect, 0 = worst
            return 1.0 - rawScore;
        }
    }

    public class EvaluationResult
    {
        public string ImagePath { get; set; } = string.Empty;
        public string GroundTruthPath { get; set; } = string.Empty;
        public string Language { get; set; } = "English";
        public string DetectedLanguage { get; set; } = "English";
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
        
        public List<string> GroundTruthMoves { get; set; } = new();
        public List<string> ExtractedMoves { get; set; } = new();
        public List<string> NormalizedMoves { get; set; } = new();
        public string GeneratedPgn { get; set; } = string.Empty;
        
        // Metrics for extracted moves
        public double ExactMatchScore { get; set; }
        public int LevenshteinDistance { get; set; }
        public double PositionalAccuracy { get; set; }
        public int LongestCommonSubsequence { get; set; }
        public double NormalizedScore { get; set; }
        
        // Metrics for normalized moves
        public double NormalizedExactMatchScore { get; set; }
        public int NormalizedLevenshteinDistance { get; set; }
        public double NormalizedPositionalAccuracy { get; set; }
        public int NormalizedLongestCommonSubsequence { get; set; }
        public double NormalizedNormalizedScore { get; set; }
        
        public void PrintSummary()
        {
            Console.WriteLine($"=== Evaluation Results ===");
            Console.WriteLine($"Image: {Path.GetFileName(ImagePath)}");
            Console.WriteLine($"Ground Truth: {Path.GetFileName(GroundTruthPath)}");
            Console.WriteLine($"Language: {Language}");
            Console.WriteLine($"Success: {IsSuccessful}");
            
            if (IsSuccessful)
            {
                Console.WriteLine($"Processing Time: {ProcessingTime.TotalSeconds:F2}s");
                Console.WriteLine($"Ground Truth Moves: {GroundTruthMoves.Count}");
                Console.WriteLine($"Extracted Moves: {ExtractedMoves.Count}");
                Console.WriteLine($"Normalized Score: {NormalizedScore:F3} (0 = perfect)");
                Console.WriteLine($"Exact Match Score: {ExactMatchScore:F3}");
                Console.WriteLine($"Positional Accuracy: {PositionalAccuracy:F3}");
                Console.WriteLine($"Levenshtein Distance: {LevenshteinDistance}");
                Console.WriteLine($"Longest Common Subsequence: {LongestCommonSubsequence}");
            }
            else
            {
                Console.WriteLine($"Error: {ErrorMessage}");
            }
            
            Console.WriteLine();
        }
    }

    public class AggregateEvaluationResult
    {
        public List<EvaluationResult> IndividualResults { get; set; } = new();
        public int TotalTestCases { get; set; }
        public int SuccessfulTestCases { get; set; }
        public double AverageNormalizedScore { get; set; }
        public double AverageExactMatchScore { get; set; }
        public double AveragePositionalAccuracy { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        
        public void PrintSummary()
        {
            Console.WriteLine($"=== Aggregate Evaluation Results ===");
            Console.WriteLine($"Total Test Cases: {TotalTestCases}");
            Console.WriteLine($"Successful Test Cases: {SuccessfulTestCases}");
            Console.WriteLine($"Success Rate: {(double)SuccessfulTestCases / TotalTestCases:P1}");
            
            if (SuccessfulTestCases > 0)
            {
                Console.WriteLine($"Average Normalized Score: {AverageNormalizedScore:F3}");
                Console.WriteLine($"Average Exact Match Score: {AverageExactMatchScore:F3}");
                Console.WriteLine($"Average Positional Accuracy: {AveragePositionalAccuracy:F3}");
                Console.WriteLine($"Average Processing Time: {AverageProcessingTime.TotalSeconds:F2}s");
            }
            
            Console.WriteLine();
        }
    }

    public class TestCase
    {
        public string ImagePath { get; set; } = string.Empty;
        public string GroundTruthPath { get; set; } = string.Empty;
        public string Language { get; set; } = "English";
    }
} 