using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ChessDecoderApi.Services;
using ChessDecoderApi.Tests.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ChessDecoderApi.Tests
{
    /// <summary>
    /// Console application for running image processing evaluations.
    /// This allows you to test the ProcessImageAsync function against ground truth data
    /// with real OpenAI API calls in a controlled manner.
    /// </summary>
    public class EvaluationRunner
    {
        /// <summary>
        /// Main entry point for running evaluations
        /// Usage: dotnet run --project Tests -- --image "path/to/image.jpg" --groundtruth "path/to/groundtruth.txt" [--language "English"]
        /// </summary>
        public static async Task Main(string[] args)
        {
            // Check if API key is available
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("ERROR: OPENAI_API_KEY environment variable is not set.");
                Console.WriteLine("Please set your OpenAI API key before running evaluations.");
                return;
            }

            Console.WriteLine("Chess Decoder Image Processing Evaluation Tool");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            try
            {
                // Parse command line arguments
                var (imagePath, groundTruthPath, language) = ParseArguments(args);

                if (string.IsNullOrEmpty(imagePath) || string.IsNullOrEmpty(groundTruthPath))
                {
                    ShowUsage();
                    return;
                }

                // Setup services
                var services = new ServiceCollection();
                services.AddHttpClient();
                services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
                
                var configuration = new ConfigurationBuilder()
                    .AddEnvironmentVariables()
                    .Build();
                services.AddSingleton<IConfiguration>(configuration);

                services.AddTransient<IImageProcessingService, ImageProcessingService>();
                services.AddTransient<ImageProcessingEvaluationService>();

                var serviceProvider = services.BuildServiceProvider();

                // Get the evaluation service
                var logger = serviceProvider.GetRequiredService<ILogger<ImageProcessingEvaluationService>>();
                var imageProcessingService = serviceProvider.GetRequiredService<IImageProcessingService>();
                var evaluationService = new ImageProcessingEvaluationService(
                    imageProcessingService, 
                    logger, 
                    useRealApi: true);

                Console.WriteLine($"Image Path: {imagePath}");
                Console.WriteLine($"Ground Truth Path: {groundTruthPath}");
                Console.WriteLine($"Language: {language}");
                Console.WriteLine();

                // Validate files exist
                if (!File.Exists(imagePath))
                {
                    Console.WriteLine($"ERROR: Image file not found: {imagePath}");
                    return;
                }

                if (!File.Exists(groundTruthPath))
                {
                    Console.WriteLine($"ERROR: Ground truth file not found: {groundTruthPath}");
                    return;
                }

                Console.WriteLine("Starting evaluation...");
                Console.WriteLine();

                // Run the evaluation
                var result = await evaluationService.EvaluateAsync(imagePath, groundTruthPath, language);

                // Display results
                Console.WriteLine("=== EVALUATION RESULTS ===");
                result.PrintSummary();

                // Display detailed comparison if available
                if (result.IsSuccessful)
                {
                    await DisplayDetailedComparison(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Run evaluation on multiple test cases
        /// </summary>
        public static async Task RunMultipleEvaluationsAsync()
        {
            // Example of how to run multiple evaluations
            var testCases = new List<TestCase>
            {
                new TestCase 
                { 
                    ImagePath = "Tests/data/test-image1.jpg", 
                    GroundTruthPath = "Tests/data/GroundTruth/Game1.txt",
                    Language = "English"
                },
                new TestCase 
                { 
                    ImagePath = "Tests/data/test-image2.jpg", 
                    GroundTruthPath = "Tests/data/GroundTruth/Game2.txt",
                    Language = "English"
                },
                // Add more test cases as needed
            };

            // Setup services (same as in Main)
            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();
            services.AddSingleton<IConfiguration>(configuration);

            services.AddTransient<IImageProcessingService, ImageProcessingService>();
            var serviceProvider = services.BuildServiceProvider();

            var logger = serviceProvider.GetRequiredService<ILogger<ImageProcessingEvaluationService>>();
            var imageProcessingService = serviceProvider.GetRequiredService<IImageProcessingService>();
            var evaluationService = new ImageProcessingEvaluationService(
                imageProcessingService, 
                logger, 
                useRealApi: true);

            Console.WriteLine("Running multiple evaluations...");
            
            var aggregateResult = await evaluationService.EvaluateMultipleAsync(testCases);
            
            Console.WriteLine("=== AGGREGATE RESULTS ===");
            aggregateResult.PrintSummary();

            // Display individual results
            Console.WriteLine("=== INDIVIDUAL RESULTS ===");
            foreach (var result in aggregateResult.IndividualResults)
            {
                result.PrintSummary();
            }
        }

        private static (string imagePath, string groundTruthPath, string language) ParseArguments(string[] args)
        {
            string imagePath = "";
            string groundTruthPath = "";
            string language = "English";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--image":
                    case "-i":
                        if (i + 1 < args.Length)
                            imagePath = args[++i];
                        break;
                    case "--groundtruth":
                    case "--gt":
                    case "-g":
                        if (i + 1 < args.Length)
                            groundTruthPath = args[++i];
                        break;
                    case "--language":
                    case "-l":
                        if (i + 1 < args.Length)
                            language = args[++i];
                        break;
                }
            }

            return (imagePath, groundTruthPath, language);
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  EvaluationRunner --image <path> --groundtruth <path> [--language <lang>]");
            Console.WriteLine();
            Console.WriteLine("Parameters:");
            Console.WriteLine("  --image, -i        Path to the chess image to process");
            Console.WriteLine("  --groundtruth, -g  Path to the ground truth PGN file");
            Console.WriteLine("  --language, -l     Language for chess notation (default: English)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  EvaluationRunner -i \"Tests/data/chess1.jpg\" -g \"Tests/data/GroundTruth/Game1.txt\"");
            Console.WriteLine("  EvaluationRunner -i \"Tests/data/greek_chess.jpg\" -g \"Tests/data/GroundTruth/Greek1.txt\" -l \"Greek\"");
            Console.WriteLine();
            Console.WriteLine("Note: Make sure OPENAI_API_KEY environment variable is set before running.");
        }

        private static async Task DisplayDetailedComparison(EvaluationResult result)
        {
            Console.WriteLine("=== DETAILED COMPARISON ===");
            Console.WriteLine($"Ground Truth ({result.GroundTruthMoves.Count} moves):");
            for (int i = 0; i < result.GroundTruthMoves.Count; i++)
            {
                Console.WriteLine($"  {i + 1,2}: {result.GroundTruthMoves[i]}");
            }

            Console.WriteLine();
            Console.WriteLine($"Extracted ({result.ExtractedMoves.Count} moves):");
            for (int i = 0; i < result.ExtractedMoves.Count; i++)
            {
                Console.WriteLine($"  {i + 1,2}: {result.ExtractedMoves[i]}");
            }

            Console.WriteLine();
            Console.WriteLine("Move-by-Move Comparison:");
            var maxMoves = Math.Max(result.GroundTruthMoves.Count, result.ExtractedMoves.Count);
            for (int i = 0; i < maxMoves; i++)
            {
                var gtMove = i < result.GroundTruthMoves.Count ? result.GroundTruthMoves[i] : "---";
                var exMove = i < result.ExtractedMoves.Count ? result.ExtractedMoves[i] : "---";
                var match = gtMove == exMove ? "✓" : "✗";
                
                Console.WriteLine($"  {i + 1,2}: {gtMove,-10} | {exMove,-10} | {match}");
            }
        }
    }
} 