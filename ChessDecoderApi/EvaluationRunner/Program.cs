using System.Net.Http.Json;
using System.Text;

namespace EvaluationRunner;

class Program
{
    private static readonly HttpClient HttpClient = new();
    private const string ApiBaseUrl = "http://localhost:5100";
    private const string EvaluationEndpoint = "/api/Evaluation/evaluate";
    private const string DualEvaluationEndpoint = "/api/Evaluation/evaluate-dual";
    private const bool AutoCrop = false;
    
    // Rate limiting: Delay between requests to avoid hitting RPM limits
    // Based on Gemini model limits:
    // - Gemini 2.5 Flash: 1,000 RPM = 60ms per request (using 100ms for safety margin)
    // - Gemini 2.0 Flash: 2,000 RPM = 30ms per request (using 50ms for safety margin)
    // Using 100ms as a conservative default to work with most models
    private const int DelayBetweenRequestsMs = 100;

    static async Task Main(string[] args)
    {
        string evaluationExamplesPath;
        
        if (args.Length > 0)
        {
            evaluationExamplesPath = args[0];
        }
        else
        {
            // Try to find the path relative to the executable or current directory
            var currentDir = Directory.GetCurrentDirectory();
            var possiblePaths = new[]
            {
                Path.Combine(currentDir, "..", "Tests", "data", "EvaluationExamples"),
                Path.Combine(currentDir, "..", "..", "Tests", "data", "EvaluationExamples"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Tests", "data", "EvaluationExamples")
            };

            evaluationExamplesPath = possiblePaths.FirstOrDefault(Directory.Exists) 
                ?? possiblePaths[0]; // Use first as default for error message
        }

        // Resolve to absolute path
        if (!Path.IsPathRooted(evaluationExamplesPath))
        {
            evaluationExamplesPath = Path.GetFullPath(evaluationExamplesPath);
        }

        if (!Directory.Exists(evaluationExamplesPath))
        {
            Console.WriteLine($"Error: Evaluation examples directory not found: {evaluationExamplesPath}");
            Console.WriteLine("Usage: EvaluationRunner.exe [path-to-EvaluationExamples]");
            return;
        }

        Console.WriteLine($"Starting evaluation run...");
        Console.WriteLine($"Scanning directory: {evaluationExamplesPath}");
        Console.WriteLine($"Rate limiting: {DelayBetweenRequestsMs}ms delay between requests to respect RPM limits");
        Console.WriteLine();

        var allResults = new List<EvaluationRunResult>();

        // Scan for language folders
        var languageFolders = Directory.GetDirectories(evaluationExamplesPath);
        
        foreach (var languageFolder in languageFolders)
        {
            var language = Path.GetFileName(languageFolder);
            Console.WriteLine($"Processing language: {language}");

            // Scan for game folders
            var gameFolders = Directory.GetDirectories(languageFolder);
            
            foreach (var gameFolder in gameFolders)
            {
                var gameName = Path.GetFileName(gameFolder);
                Console.WriteLine($"  Processing game: {gameName}");

                var result = await ProcessGameFolder(gameFolder, language, gameName);
                allResults.Add(result);
                
                if (result.IsSuccessful)
                {
                    Console.WriteLine($"    ✓ Success - Score: {result.NormalizedScore:F3}");
                }
                else
                {
                    Console.WriteLine($"    ✗ Failed - {result.ErrorMessage}");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("Generating HTML report...");

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "evaluation-report.html");
        await GenerateHtmlReport(allResults, outputPath);

        Console.WriteLine($"Report generated: {outputPath}");
        Console.WriteLine();
        PrintSummary(allResults);
    }

    static async Task<EvaluationRunResult> ProcessGameFolder(string gameFolder, string language, string gameName)
    {
        var result = new EvaluationRunResult
        {
            Language = language,
            GameName = gameName,
            GameFolder = gameFolder
        };

        // Find image files - check for multi-page games (e.g., Game11.jpg and Game11_2.jpg)
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg" };
        var imageFiles = Directory.GetFiles(gameFolder)
            .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();

        if (imageFiles.Count == 0)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = "No image file found";
            return result;
        }

        // Detect multi-page games by checking for _2 suffix pattern
        var (page1File, page2File) = DetectMultiPageImages(imageFiles, gameName);
        var isMultiPage = page2File != null;

        if (isMultiPage)
        {
            Console.WriteLine($"      Multi-page game detected: {Path.GetFileName(page1File)} + {Path.GetFileName(page2File)}");
        }

        // Find text file
        var textFile = Directory.GetFiles(gameFolder)
            .FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() == ".txt");

        if (textFile == null)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = "No ground truth text file found";
            return result;
        }

        // Call evaluation API
        try
        {
            if (isMultiPage && page2File != null)
            {
                return await ProcessDualPageEvaluation(result, page1File, page2File, textFile, language);
            }
            else
            {
                return await ProcessSinglePageEvaluation(result, page1File, textFile, language);
            }
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.ErrorMessage = $"Exception: {ex.Message}";
        }

        return result;
    }

    static (string page1, string? page2) DetectMultiPageImages(List<string> imageFiles, string gameName)
    {
        // Look for patterns like: Game11.jpg + Game11_2.jpg or Game11_page1.jpg + Game11_page2.jpg
        if (imageFiles.Count < 2)
        {
            return (imageFiles[0], null);
        }

        // Sort files to find primary and secondary
        var primaryPattern = new[] { "_1", "_page1", "page1" };
        var secondaryPattern = new[] { "_2", "_page2", "page2" };

        string? page1 = null;
        string? page2 = null;

        foreach (var file in imageFiles)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            
            // Check if it's explicitly marked as page 2
            if (secondaryPattern.Any(p => fileNameWithoutExt.EndsWith(p)))
            {
                page2 = file;
            }
            // Check if it's explicitly marked as page 1
            else if (primaryPattern.Any(p => fileNameWithoutExt.EndsWith(p)))
            {
                page1 = file;
            }
            // Otherwise, if we have 2 files and one ends with _2, the other is page 1
            else if (page1 == null)
            {
                page1 = file;
            }
        }

        // If we found a page2 but no explicit page1, use the first file as page1
        if (page2 != null && page1 == null)
        {
            page1 = imageFiles.FirstOrDefault(f => f != page2) ?? imageFiles[0];
        }

        return (page1 ?? imageFiles[0], page2);
    }

    static async Task<EvaluationRunResult> ProcessSinglePageEvaluation(
        EvaluationRunResult result, 
        string imageFile, 
        string textFile, 
        string language)
    {
        using var content = new MultipartFormDataContent();
        
        // Add image file
        var imageBytes = await File.ReadAllBytesAsync(imageFile);
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/" + Path.GetExtension(imageFile).TrimStart('.').ToLowerInvariant());
        content.Add(imageContent, "Image", Path.GetFileName(imageFile));

        // Add ground truth file
        var textBytes = await File.ReadAllBytesAsync(textFile);
        var textContent = new ByteArrayContent(textBytes);
        textContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(textContent, "GroundTruth", Path.GetFileName(textFile));

        // Add other parameters
        content.Add(new StringContent(language), "Language");
        content.Add(new StringContent(AutoCrop.ToString().ToLowerInvariant()), "Autocrop");

        var response = await HttpClient.PostAsync($"{ApiBaseUrl}{EvaluationEndpoint}", content);
        
        if (response.IsSuccessStatusCode)
        {
            var evaluationResult = await response.Content.ReadFromJsonAsync<EvaluationResultResponse>();
            if (evaluationResult != null)
            {
                PopulateResultFromResponse(result, evaluationResult, language);
            }
            else
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "Failed to parse response";
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            result.IsSuccessful = false;
            result.ErrorMessage = $"API error: {response.StatusCode} - {errorContent}";
        }
        
        // Rate limiting: Wait before next request to avoid hitting RPM limits
        await Task.Delay(DelayBetweenRequestsMs);

        return result;
    }

    static async Task<EvaluationRunResult> ProcessDualPageEvaluation(
        EvaluationRunResult result, 
        string page1File, 
        string page2File, 
        string textFile, 
        string language)
    {
        using var content = new MultipartFormDataContent();
        
        // Add page 1 image
        var page1Bytes = await File.ReadAllBytesAsync(page1File);
        var page1Content = new ByteArrayContent(page1Bytes);
        page1Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/" + Path.GetExtension(page1File).TrimStart('.').ToLowerInvariant());
        content.Add(page1Content, "Page1", Path.GetFileName(page1File));

        // Add page 2 image
        var page2Bytes = await File.ReadAllBytesAsync(page2File);
        var page2Content = new ByteArrayContent(page2Bytes);
        page2Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/" + Path.GetExtension(page2File).TrimStart('.').ToLowerInvariant());
        content.Add(page2Content, "Page2", Path.GetFileName(page2File));

        // Add ground truth file
        var textBytes = await File.ReadAllBytesAsync(textFile);
        var textContent = new ByteArrayContent(textBytes);
        textContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(textContent, "GroundTruth", Path.GetFileName(textFile));

        // Add other parameters
        content.Add(new StringContent(language), "Language");
        content.Add(new StringContent(AutoCrop.ToString().ToLowerInvariant()), "Autocrop");

        var response = await HttpClient.PostAsync($"{ApiBaseUrl}{DualEvaluationEndpoint}", content);
        
        if (response.IsSuccessStatusCode)
        {
            var evaluationResult = await response.Content.ReadFromJsonAsync<EvaluationResultResponse>();
            if (evaluationResult != null)
            {
                PopulateResultFromResponse(result, evaluationResult, language);
                result.IsMultiPage = true;
            }
            else
            {
                result.IsSuccessful = false;
                result.ErrorMessage = "Failed to parse response";
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            result.IsSuccessful = false;
            result.ErrorMessage = $"API error: {response.StatusCode} - {errorContent}";
        }
        
        // Rate limiting: Wait before next request to avoid hitting RPM limits
        await Task.Delay(DelayBetweenRequestsMs);

        return result;
    }

    static void PopulateResultFromResponse(EvaluationRunResult result, EvaluationResultResponse evaluationResult, string language)
    {
        result.IsSuccessful = evaluationResult.IsSuccessful;
        result.ErrorMessage = evaluationResult.ErrorMessage;
        result.NormalizedScore = evaluationResult.Metrics.NormalizedScore;
        result.ExactMatchScore = evaluationResult.Metrics.ExactMatchScore;
        result.PositionalAccuracy = evaluationResult.Metrics.PositionalAccuracy;
        result.LevenshteinDistance = evaluationResult.Metrics.LevenshteinDistance;
        result.LongestCommonSubsequence = evaluationResult.Metrics.LongestCommonSubsequence;
        result.ProcessingTimeSeconds = evaluationResult.ProcessingTimeSeconds;
        result.GroundTruthMoves = evaluationResult.MoveCounts.GroundTruthMoves;
        result.ExtractedMoves = evaluationResult.MoveCounts.ExtractedMoves;
        result.ImageFileName = evaluationResult.ImageFileName;
        result.GroundTruthFileName = evaluationResult.GroundTruthFileName;
        result.GeneratedPgn = evaluationResult.GeneratedPgn;
        result.GroundTruthMoveList = evaluationResult.Moves.GroundTruth;
        result.ExtractedMoveList = evaluationResult.Moves.Extracted;
        result.DetectedLanguage = evaluationResult.DetectedLanguage ?? language;
        
        // Normalized move metrics
        if (evaluationResult.NormalizedMetrics != null)
        {
            result.NormalizedNormalizedScore = evaluationResult.NormalizedMetrics.NormalizedScore;
            result.NormalizedExactMatchScore = evaluationResult.NormalizedMetrics.ExactMatchScore;
            result.NormalizedPositionalAccuracy = evaluationResult.NormalizedMetrics.PositionalAccuracy;
            result.NormalizedLevenshteinDistance = evaluationResult.NormalizedMetrics.LevenshteinDistance;
            result.NormalizedLongestCommonSubsequence = evaluationResult.NormalizedMetrics.LongestCommonSubsequence;
            result.NormalizedMoves = evaluationResult.MoveCounts.NormalizedMoves;
            result.NormalizedMoveList = evaluationResult.Moves.Normalized;
        }
    }

    static async Task GenerateHtmlReport(List<EvaluationRunResult> results, string outputPath)
    {
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("    <title>Chess Decoder Evaluation Report</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
        html.AppendLine("        .container { max-width: 1400px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("        h1 { color: #333; border-bottom: 3px solid #4CAF50; padding-bottom: 10px; }");
        html.AppendLine("        h2 { color: #555; margin-top: 30px; }");
        html.AppendLine("        .summary { background: #f9f9f9; padding: 20px; border-radius: 5px; margin: 20px 0; }");
        html.AppendLine("        .summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin-top: 15px; }");
        html.AppendLine("        .summary-item { background: white; padding: 15px; border-radius: 5px; border-left: 4px solid #4CAF50; }");
        html.AppendLine("        .summary-item h3 { margin: 0 0 10px 0; color: #333; font-size: 14px; }");
        html.AppendLine("        .summary-item .value { font-size: 24px; font-weight: bold; color: #4CAF50; }");
        html.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 20px 0; }");
        html.AppendLine("        th, td { padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }");
        html.AppendLine("        th { background-color: #4CAF50; color: white; font-weight: bold; }");
        html.AppendLine("        tr:hover { background-color: #f5f5f5; }");
        html.AppendLine("        .success { color: #4CAF50; font-weight: bold; }");
        html.AppendLine("        .failure { color: #f44336; font-weight: bold; }");
        html.AppendLine("        .score { font-size: 18px; font-weight: bold; }");
        html.AppendLine("        .score-high { color: #4CAF50; }");
        html.AppendLine("        .score-medium { color: #FF9800; }");
        html.AppendLine("        .score-low { color: #f44336; }");
        html.AppendLine("        .details { margin-top: 20px; }");
        html.AppendLine("        .details-section { margin: 20px 0; padding: 15px; background: #f9f9f9; border-radius: 5px; }");
        html.AppendLine("        .details-section h3 { margin-top: 0; color: #555; }");
        html.AppendLine("        .moves-comparison { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }");
        html.AppendLine("        .moves-list { background: white; padding: 10px; border-radius: 5px; max-height: 300px; overflow-y: auto; }");
        html.AppendLine("        .move-item { padding: 5px; margin: 2px 0; }");
        html.AppendLine("        .move-match { background: #c8e6c9; }");
        html.AppendLine("        .move-diff { background: #ffcdd2; }");
        html.AppendLine("        .language-section { margin: 30px 0; }");
        html.AppendLine("        .language-header { background: #2196F3; color: white; padding: 15px; border-radius: 5px 5px 0 0; }");
        html.AppendLine("        .timestamp { color: #777; font-size: 12px; margin-bottom: 20px; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container\">");
        html.AppendLine($"        <h1>Chess Decoder Evaluation Report</h1>");
        html.AppendLine($"        <div class=\"timestamp\">Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");

        // Global Summary for Extracted Moves
        var successfulResults = results.Where(r => r.IsSuccessful).ToList();
        var totalTests = results.Count;
        var successCount = successfulResults.Count;
        var avgNormalizedScore = successfulResults.Any() ? successfulResults.Average(r => r.NormalizedScore) : 0;
        var avgExactMatch = successfulResults.Any() ? successfulResults.Average(r => r.ExactMatchScore) : 0;
        var avgPositionalAccuracy = successfulResults.Any() ? successfulResults.Average(r => r.PositionalAccuracy) : 0;
        var avgProcessingTime = successfulResults.Any() ? successfulResults.Average(r => r.ProcessingTimeSeconds) : 0;

        AppendGlobalSummary(html, "Extracted Moves", totalTests, successCount, avgNormalizedScore, avgExactMatch, avgPositionalAccuracy, avgProcessingTime);

        // Global Summary for Normalized Moves
        var resultsWithNormalized = results.Where(r => r.IsSuccessful && r.NormalizedMoveList.Count > 0).ToList();
        if (resultsWithNormalized.Any())
        {
            var avgNormalizedNormalizedScore = resultsWithNormalized.Average(r => r.NormalizedNormalizedScore);
            var avgNormalizedExactMatch = resultsWithNormalized.Average(r => r.NormalizedExactMatchScore);
            var avgNormalizedPositionalAccuracy = resultsWithNormalized.Average(r => r.NormalizedPositionalAccuracy);

            AppendGlobalSummary(html, "Normalized Moves", resultsWithNormalized.Count, null, avgNormalizedNormalizedScore, avgNormalizedExactMatch, avgNormalizedPositionalAccuracy, null);
        }

        // Language Detection Accuracy Summary
        AppendLanguageDetectionSummary(html, results);

        // Results by Language - Extracted Moves
        var resultsByLanguage = results.GroupBy(r => r.Language).OrderBy(g => g.Key);
        
        foreach (var languageGroup in resultsByLanguage)
        {
            AppendLanguageResultsTable(html, languageGroup.Key, languageGroup, "Extracted Moves", isNormalized: false);
        }

        // Results by Language - Normalized Moves
        foreach (var languageGroup in resultsByLanguage)
        {
            var languageResultsWithNormalized = languageGroup.Where(r => r.IsSuccessful && r.NormalizedMoveList.Count > 0).ToList();
            if (!languageResultsWithNormalized.Any()) continue;

            AppendLanguageResultsTable(html, languageGroup.Key, languageResultsWithNormalized, "Normalized Moves", isNormalized: true);
        }

        // Detailed Results - Extracted Moves
        html.AppendLine("        <div class=\"details\">");
        html.AppendLine("            <h2>Detailed Results - Extracted Moves</h2>");

        foreach (var result in results.OrderBy(r => r.Language).ThenBy(r => r.GameName))
        {
            html.AppendLine($"            <div class=\"details-section\">");
            html.AppendLine($"                <h3>{result.Language} - {result.GameName}</h3>");
            
            if (result.IsSuccessful)
            {
                html.AppendLine("                <div class=\"moves-comparison\">");
                html.AppendLine("                    <div>");
                html.AppendLine("                        <h4>Ground Truth Moves</h4>");
                html.AppendLine("                        <div class=\"moves-list\">");
                foreach (var move in result.GroundTruthMoveList)
                {
                    var isMatch = result.ExtractedMoveList.Contains(move);
                    html.AppendLine($"                            <div class=\"move-item {(isMatch ? "move-match" : "move-diff")}\">{move}</div>");
                }
                html.AppendLine("                        </div>");
                html.AppendLine("                    </div>");
                html.AppendLine("                    <div>");
                html.AppendLine("                        <h4>Extracted Moves</h4>");
                html.AppendLine("                        <div class=\"moves-list\">");
                foreach (var move in result.ExtractedMoveList)
                {
                    var isMatch = result.GroundTruthMoveList.Contains(move);
                    html.AppendLine($"                            <div class=\"move-item {(isMatch ? "move-match" : "move-diff")}\">{move}</div>");
                }
                html.AppendLine("                        </div>");
                html.AppendLine("                    </div>");
                html.AppendLine("                </div>");
                
                if (!string.IsNullOrEmpty(result.GeneratedPgn))
                {
                    html.AppendLine("                <div style=\"margin-top: 15px;\">");
                    html.AppendLine("                    <h4>Generated PGN</h4>");
                    html.AppendLine($"                    <pre style=\"background: white; padding: 10px; border-radius: 5px; overflow-x: auto;\">{result.GeneratedPgn}</pre>");
                    html.AppendLine("                </div>");
                }
            }
            else
            {
                html.AppendLine($"                <p class=\"failure\">Error: {result.ErrorMessage}</p>");
            }
            
            html.AppendLine("            </div>");
        }

        html.AppendLine("        </div>");

        // Detailed Results - Normalized Moves
        var resultsWithNormalizedDetails = results.Where(r => r.IsSuccessful && r.NormalizedMoveList.Count > 0).ToList();
        if (resultsWithNormalizedDetails.Any())
        {
            html.AppendLine("        <div class=\"details\">");
            html.AppendLine("            <h2>Detailed Results - Normalized Moves</h2>");

            foreach (var result in resultsWithNormalizedDetails.OrderBy(r => r.Language).ThenBy(r => r.GameName))
            {
                html.AppendLine($"            <div class=\"details-section\">");
                html.AppendLine($"                <h3>{result.Language} - {result.GameName}</h3>");
                
                html.AppendLine("                <div class=\"moves-comparison\">");
                html.AppendLine("                    <div>");
                html.AppendLine("                        <h4>Ground Truth Moves</h4>");
                html.AppendLine("                        <div class=\"moves-list\">");
                foreach (var move in result.GroundTruthMoveList)
                {
                    var isMatch = result.NormalizedMoveList.Contains(move);
                    html.AppendLine($"                            <div class=\"move-item {(isMatch ? "move-match" : "move-diff")}\">{move}</div>");
                }
                html.AppendLine("                        </div>");
                html.AppendLine("                    </div>");
                html.AppendLine("                    <div>");
                html.AppendLine("                        <h4>Normalized Moves</h4>");
                html.AppendLine("                        <div class=\"moves-list\">");
                foreach (var move in result.NormalizedMoveList)
                {
                    var isMatch = result.GroundTruthMoveList.Contains(move);
                    html.AppendLine($"                            <div class=\"move-item {(isMatch ? "move-match" : "move-diff")}\">{move}</div>");
                }
                html.AppendLine("                        </div>");
                html.AppendLine("                    </div>");
                html.AppendLine("                </div>");
                
                html.AppendLine("            </div>");
            }

            html.AppendLine("        </div>");
        }

        html.AppendLine("    </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(outputPath, html.ToString());
    }

    static void AppendGlobalSummary(StringBuilder html, string moveType, int totalTests, int? successCount, double avgNormalizedScore, double avgExactMatch, double avgPositionalAccuracy, double? avgProcessingTime)
    {
        html.AppendLine("        <div class=\"summary\">");
        html.AppendLine($"            <h2>Global Summary - {moveType}</h2>");
        html.AppendLine("            <div class=\"summary-grid\">");
        html.AppendLine($"                <div class=\"summary-item\"><h3>Total Tests</h3><div class=\"value\">{totalTests}</div></div>");
        
        if (successCount.HasValue)
        {
            html.AppendLine($"                <div class=\"summary-item\"><h3>Successful</h3><div class=\"value\">{successCount.Value}</div></div>");
            html.AppendLine($"                <div class=\"summary-item\"><h3>Success Rate</h3><div class=\"value\">{(successCount.Value * 100.0 / totalTests):F1}%</div></div>");
        }
        
        html.AppendLine($"                <div class=\"summary-item\"><h3>Avg Normalized Score</h3><div class=\"value\">{avgNormalizedScore:F3}</div></div>");
        html.AppendLine($"                <div class=\"summary-item\"><h3>Avg Exact Match</h3><div class=\"value\">{avgExactMatch:F3}</div></div>");
        html.AppendLine($"                <div class=\"summary-item\"><h3>Avg Positional Accuracy</h3><div class=\"value\">{avgPositionalAccuracy:F3}</div></div>");
        
        if (avgProcessingTime.HasValue)
        {
            html.AppendLine($"                <div class=\"summary-item\"><h3>Avg Processing Time</h3><div class=\"value\">{avgProcessingTime.Value:F2}s</div></div>");
        }
        
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
    }

    static void AppendLanguageDetectionSummary(StringBuilder html, List<EvaluationRunResult> results)
    {
        var successfulResults = results.Where(r => r.IsSuccessful && !string.IsNullOrEmpty(r.DetectedLanguage)).ToList();
        var totalTests = successfulResults.Count;
        
        if (totalTests == 0)
        {
            return;
        }

        // Calculate accuracy
        var correctDetections = successfulResults.Count(r => 
            string.Equals(r.DetectedLanguage, r.Language, StringComparison.OrdinalIgnoreCase));
        var accuracy = totalTests > 0 ? (double)correctDetections / totalTests : 0.0;

        html.AppendLine("        <div class=\"summary\">");
        html.AppendLine("            <h2>Language Detection Accuracy</h2>");
        html.AppendLine("            <div class=\"summary-grid\">");
        html.AppendLine($"                <div class=\"summary-item\"><h3>Total Tests</h3><div class=\"value\">{totalTests}</div></div>");
        html.AppendLine($"                <div class=\"summary-item\"><h3>Correct Detections</h3><div class=\"value\">{correctDetections}</div></div>");
        html.AppendLine($"                <div class=\"summary-item\"><h3>Accuracy</h3><div class=\"value\">{(accuracy * 100.0):F1}%</div></div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");

        // Language Detection Table
        html.AppendLine("        <div class=\"language-section\">");
        html.AppendLine("            <div class=\"language-header\"><h2>Language Detection Results</h2></div>");
        html.AppendLine("            <table>");
        html.AppendLine("                <thead>");
        html.AppendLine("                    <tr>");
        html.AppendLine("                        <th>Language</th>");
        html.AppendLine("                        <th>Game</th>");
        html.AppendLine("                        <th>Expected Language</th>");
        html.AppendLine("                        <th>Detected Language</th>");
        html.AppendLine("                        <th>Status</th>");
        html.AppendLine("                    </tr>");
        html.AppendLine("                </thead>");
        html.AppendLine("                <tbody>");

        foreach (var result in successfulResults.OrderBy(r => r.Language).ThenBy(r => r.GameName))
        {
            var isCorrect = string.Equals(result.DetectedLanguage, result.Language, StringComparison.OrdinalIgnoreCase);
            var statusClass = isCorrect ? "success" : "failure";
            var statusText = isCorrect ? "✓ Correct" : "✗ Incorrect";

            html.AppendLine("                    <tr>");
            html.AppendLine($"                        <td><strong>{result.Language}</strong></td>");
            html.AppendLine($"                        <td>{result.GameName}</td>");
            html.AppendLine($"                        <td>{result.Language}</td>");
            html.AppendLine($"                        <td>{result.DetectedLanguage}</td>");
            html.AppendLine($"                        <td class=\"{statusClass}\">{statusText}</td>");
            html.AppendLine("                    </tr>");
        }

        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");
        html.AppendLine("        </div>");
    }

    static void AppendLanguageResultsTable(StringBuilder html, string language, IEnumerable<EvaluationRunResult> results, string moveType, bool isNormalized)
    {
        html.AppendLine($"        <div class=\"language-section\">");
        html.AppendLine($"            <div class=\"language-header\"><h2>{language} Language Results - {moveType}</h2></div>");
        html.AppendLine("            <table>");
        html.AppendLine("                <thead>");
        html.AppendLine("                    <tr>");
        html.AppendLine("                        <th>Game</th>");
        html.AppendLine("                        <th>Status</th>");
        html.AppendLine("                        <th>Normalized Score</th>");
        html.AppendLine("                        <th>Exact Match</th>");
        html.AppendLine("                        <th>Positional Accuracy</th>");
        html.AppendLine("                        <th>Levenshtein Distance</th>");
        html.AppendLine("                        <th>LCS</th>");
                    html.AppendLine("                        <th>Ground Truth Moves</th>");
                    html.AppendLine($"                        <th>{moveType}</th>");
                    html.AppendLine("                        <th>Detected Language</th>");
                    html.AppendLine("                        <th>Processing Time</th>");
        html.AppendLine("                    </tr>");
        html.AppendLine("                </thead>");
        html.AppendLine("                <tbody>");

        foreach (var result in results.OrderBy(r => r.GameName))
        {
            double score;
            string scoreClass;
            
            if (isNormalized)
            {
                score = result.NormalizedNormalizedScore;
                scoreClass = score >= 0.8 ? "score-high" : score >= 0.5 ? "score-medium" : "score-low";
            }
            else
            {
                score = result.NormalizedScore;
                scoreClass = result.IsSuccessful 
                    ? (score >= 0.8 ? "score-high" : score >= 0.5 ? "score-medium" : "score-low")
                    : "";
            }

            html.AppendLine("                    <tr>");
            html.AppendLine($"                        <td><strong>{result.GameName}</strong></td>");
            
            if (isNormalized)
            {
                html.AppendLine($"                        <td class=\"success\">✓ Success</td>");
            }
            else
            {
                html.AppendLine($"                        <td class=\"{(result.IsSuccessful ? "success" : "failure")}\">{(result.IsSuccessful ? "✓ Success" : "✗ Failed")}</td>");
            }

            if (result.IsSuccessful)
            {
                html.AppendLine($"                        <td class=\"score {scoreClass}\">{score:F3}</td>");
                
                if (isNormalized)
                {
                    html.AppendLine($"                        <td>{result.NormalizedExactMatchScore:F3}</td>");
                    html.AppendLine($"                        <td>{result.NormalizedPositionalAccuracy:F3}</td>");
                    html.AppendLine($"                        <td>{result.NormalizedLevenshteinDistance}</td>");
                    html.AppendLine($"                        <td>{result.NormalizedLongestCommonSubsequence}</td>");
                    html.AppendLine($"                        <td>{result.GroundTruthMoves}</td>");
                    html.AppendLine($"                        <td>{result.NormalizedMoves}</td>");
                }
                else
                {
                    html.AppendLine($"                        <td>{result.ExactMatchScore:F3}</td>");
                    html.AppendLine($"                        <td>{result.PositionalAccuracy:F3}</td>");
                    html.AppendLine($"                        <td>{result.LevenshteinDistance}</td>");
                    html.AppendLine($"                        <td>{result.LongestCommonSubsequence}</td>");
                    html.AppendLine($"                        <td>{result.GroundTruthMoves}</td>");
                    html.AppendLine($"                        <td>{result.ExtractedMoves}</td>");
                }
                
                var detectedLangStatus = string.Equals(result.DetectedLanguage, result.Language, StringComparison.OrdinalIgnoreCase) 
                    ? $"<span class=\"success\">{result.DetectedLanguage}</span>" 
                    : $"<span class=\"failure\">{result.DetectedLanguage}</span>";
                html.AppendLine($"                        <td>{detectedLangStatus}</td>");
                html.AppendLine($"                        <td>{result.ProcessingTimeSeconds:F2}s</td>");
            }
            else
            {
                html.AppendLine($"                        <td colspan=\"9\">{result.ErrorMessage}</td>");
            }

            html.AppendLine("                    </tr>");
        }

        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");
        html.AppendLine("        </div>");
    }

    static void PrintSummary(List<EvaluationRunResult> results)
    {
        var successfulResults = results.Where(r => r.IsSuccessful).ToList();
        var totalTests = results.Count;
        var successCount = successfulResults.Count;

        Console.WriteLine("=== Evaluation Summary ===");
        Console.WriteLine($"Total Tests: {totalTests}");
        Console.WriteLine($"Successful: {successCount}");
        Console.WriteLine($"Failed: {totalTests - successCount}");
        Console.WriteLine($"Success Rate: {(successCount * 100.0 / totalTests):F1}%");
        
        if (successfulResults.Any())
        {
            Console.WriteLine($"Average Normalized Score: {successfulResults.Average(r => r.NormalizedScore):F3}");
            Console.WriteLine($"Average Exact Match Score: {successfulResults.Average(r => r.ExactMatchScore):F3}");
            Console.WriteLine($"Average Positional Accuracy: {successfulResults.Average(r => r.PositionalAccuracy):F3}");
            Console.WriteLine($"Average Processing Time: {successfulResults.Average(r => r.ProcessingTimeSeconds):F2}s");
        }
    }
}

class EvaluationRunResult
{
    public string Language { get; set; } = string.Empty;
    public string DetectedLanguage { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string GameFolder { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public bool IsMultiPage { get; set; }
    public string? ErrorMessage { get; set; }
    public double NormalizedScore { get; set; }
    public double ExactMatchScore { get; set; }
    public double PositionalAccuracy { get; set; }
    public int LevenshteinDistance { get; set; }
    public int LongestCommonSubsequence { get; set; }
    public double ProcessingTimeSeconds { get; set; }
    public int GroundTruthMoves { get; set; }
    public int ExtractedMoves { get; set; }
    public string ImageFileName { get; set; } = string.Empty;
    public string GroundTruthFileName { get; set; } = string.Empty;
    public string? GeneratedPgn { get; set; }
    public List<string> GroundTruthMoveList { get; set; } = new();
    public List<string> ExtractedMoveList { get; set; } = new();
    
    // Normalized move metrics
    public double NormalizedNormalizedScore { get; set; }
    public double NormalizedExactMatchScore { get; set; }
    public double NormalizedPositionalAccuracy { get; set; }
    public int NormalizedLevenshteinDistance { get; set; }
    public int NormalizedLongestCommonSubsequence { get; set; }
    public int NormalizedMoves { get; set; }
    public List<string> NormalizedMoveList { get; set; } = new();
}

// DTOs matching the API response
class EvaluationResultResponse
{
    public string ImageFileName { get; set; } = string.Empty;
    public string GroundTruthFileName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? DetectedLanguage { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public double ProcessingTimeSeconds { get; set; }
    public EvaluationMetricsDto Metrics { get; set; } = new();
    public EvaluationMetricsDto? NormalizedMetrics { get; set; }
    public MoveCountsDto MoveCounts { get; set; } = new();
    public MovesDto Moves { get; set; } = new();
    public string? GeneratedPgn { get; set; }
}

class EvaluationMetricsDto
{
    public double NormalizedScore { get; set; }
    public double ExactMatchScore { get; set; }
    public double PositionalAccuracy { get; set; }
    public int LevenshteinDistance { get; set; }
    public int LongestCommonSubsequence { get; set; }
}

class MoveCountsDto
{
    public int GroundTruthMoves { get; set; }
    public int ExtractedMoves { get; set; }
    public int NormalizedMoves { get; set; }
}

class MovesDto
{
    public List<string> GroundTruth { get; set; } = new();
    public List<string> Extracted { get; set; } = new();
    public List<string> Normalized { get; set; } = new();
}

