using ChessDecoderApi.DTOs;
using ChessDecoderApi.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace ChessDecoderApi.Services
{
    public class ImageProcessingService : IImageProcessingService
    {	   
        private readonly IHttpClientFactory _httpClientFactory;
	    private readonly IConfiguration _configuration;
        private readonly ILogger<ImageProcessingService> _logger;
	    private readonly ILoggerFactory _loggerFactory;
        
        private readonly IChessMoveProcessor _chessMoveProcessor;
        private readonly IChessMoveValidator _chessMoveValidator;

        private static readonly Dictionary<string, string> GreekToEnglishMap = new()
        {
            { "Π", "R" }, // Πύργος (Rook)
            { "Α", "B" }, // Αξιωματικός (Bishop)
            { "A", "B" }, // Αξιωματικός (Bishop)
            { "Β", "Q" }, // Βασίλισσα (Queen)
            // { "B", "Q" }, // Βασίλισσα (Queen) - Don't add english B as well, as it transforms the Bishop to Queen...
            { "Ι", "N" }, // Ιππος (Knight)
            { "I", "N" }, // Ιππος (Knight)
            { "Ρ", "K" }, // Ρήγας (King)
            { "P", "K" }, // Ρήγας (King)
            { "0", "0" }, // Castling short
            { "Ο", "0" }, // Castling short
            { "O", "0" }, // Castling short
            { "χ", "x" }, // Capture
            { "+", "+" }, // Check
            { "#", "#" }, // Checkmate
            { "α", "a" }, // File letters
            { "β", "b" },
            { "γ", "c" },
            { "δ", "d" },
            { "ε", "e" },
            { "ζ", "f" },
            { "η", "g" },
            { "θ", "h" }
        };

        public ImageProcessingService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ImageProcessingService> logger,
            ILoggerFactory loggerFactory,
            IChessMoveProcessor chessMoveProcessor,
            IChessMoveValidator chessMoveValidator
            )
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _chessMoveProcessor = chessMoveProcessor ?? throw new ArgumentNullException(nameof(chessMoveProcessor));
            _chessMoveValidator = chessMoveValidator ?? throw new ArgumentNullException(nameof(chessMoveValidator));
        }

        /// <summary>
        /// Automatically detects the language of chess notation in an image
        /// </summary>
        /// <param name="imagePath">Path to the image file</param>
        /// <returns>Detected language ("Greek" or "English", defaults to "English")</returns>
        public async Task<string> DetectLanguageAsync(string imagePath)
        {
            try
            {
                Image<Rgba32> image;
                
                // Check if it's a URL or local file path
                if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    // Download image from URL
                    using var httpClient = _httpClientFactory.CreateClient();
                    var downloadedBytes = await httpClient.GetByteArrayAsync(imagePath);
                    image = Image.Load<Rgba32>(downloadedBytes);
                }
                else
                {
                    // Local file path
                    if (!File.Exists(imagePath))
                    {
                        _logger.LogWarning("Image file not found for language detection, defaulting to English: {ImagePath}", imagePath);
                        return "English";
                    }
                    image = Image.Load<Rgba32>(imagePath);
                }

                // Convert image to bytes for OCR
                byte[] imageBytes;
                using (var ms = new MemoryStream())
                {
                    image.SaveAsJpeg(ms);
                    imageBytes = ms.ToArray();
                }

                // Perform language detection using Gemini API with JSON response
                string jsonResponse = await ExtractTextFromImageForDetectionAsync(imageBytes);

                // Parse the JSON response to extract language
                if (string.IsNullOrWhiteSpace(jsonResponse))
                {
                    _logger.LogWarning("Empty response from language detection, defaulting to English");
                    return "English";
                }

                try
                {
                    using var jsonDoc = JsonDocument.Parse(jsonResponse);
                    if (jsonDoc.RootElement.TryGetProperty("language", out var languageElement))
                    {
                        string detectedLanguage = languageElement.GetString() ?? "English";
                        _logger.LogInformation("Detected language: {Language}", detectedLanguage);
                        return detectedLanguage;
                    }
                    else
                    {
                        _logger.LogWarning("Language property not found in JSON response, defaulting to English");
                        return "English";
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON response from language detection, defaulting to English");
                    return "English";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during language detection, defaulting to English");
                return "English";
            }
        }

        /// <summary>
        /// Performs a quick OCR pass on the image to extract sample text for language detection
        /// </summary>
        private async Task<string> ExtractTextFromImageForDetectionAsync(byte[] imageBytes)
        {
            try
            {
                // Use Gemini for quick detection (faster than full extraction)
                string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? 
                    _configuration["GEMINI_API_KEY"] ?? 
                    throw new UnauthorizedAccessException("GEMINI_API_KEY environment variable not set");

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
                
                var base64Image = Convert.ToBase64String(imageBytes);

                // Prompt for language detection with JSON response
                var promptText = @"I am uploading a chess game notation file detect in which language the CHESS MOVES are written

return the result in the following json format::

{
  ""language"": ""English"",
  ""notation_system"": ""Standard Algebraic Notation""
}";

                var requestData = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = promptText },
                                new
                                {
                                    inline_data = new
                                    {
                                        mime_type = "image/jpeg",
                                        data = base64Image
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = 500, // Small token limit for quick detection
                        response_mime_type = "application/json",
                        thinkingConfig = new
                        {
                            thinkingLevel = "low"
                        }
                    }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json");

                var response = await client.PostAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-preview:generateContent", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Language detection OCR failed, defaulting to English");
                    return "";
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseContent);
                
                if (!jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    return "";
                }

                var firstCandidate = candidates[0];
                if (!firstCandidate.TryGetProperty("content", out var contentElement) || contentElement.ValueKind == JsonValueKind.Null)
                {
                    return "";
                }

                if (!contentElement.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                {
                    return "";
                }

                var firstPart = parts[0];
                if (!firstPart.TryGetProperty("text", out var textElement))
                {
                    return "";
                }

                return textElement.GetString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in quick OCR for language detection");
                return "";
            }
        }

        /// <summary>
        /// Extracts chess moves from an image by sending the whole image to LLM.
        /// The LLM is instructed to parse columns and return moves in JSON format.
        /// </summary>
        /// <param name="imagePath">Path to the chess image or URL</param>
        /// <returns>Tuple of two lists: whiteMoves and blackMoves</returns>
        public virtual async Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(string imagePath)
        {
            // Auto-detect language before processing
            string language = await DetectLanguageAsync(imagePath);
            _logger.LogInformation($"Auto-detected language: {language} for image: {imagePath}");

            Image<Rgba32> image;
            
            // Check if it's a URL or local file path
            if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                // Download image from URL
                using var httpClient = _httpClientFactory.CreateClient();
                var downloadedBytes = await httpClient.GetByteArrayAsync(imagePath);
                image = Image.Load<Rgba32>(downloadedBytes);
            }
            else
            {
                // Local file path
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException("Image file not found", imagePath);
                }
                image = Image.Load<Rgba32>(imagePath);
            }

            // Convert whole image to bytes
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                image.SaveAsJpeg(ms);
                imageBytes = ms.ToArray();
            }

            _logger.LogInformation($"Processing whole image (no column splitting)");

            // Extract text from whole image with column-aware prompt
            string jsonResponse = await ExtractTextFromImageAsyncWholeImage(imageBytes, language);
            
            // Parse JSON response to extract moves from columns
            var (whiteMoves, blackMoves) = await ParseMovesFromColumnJsonAsync(jsonResponse, language);

            return (whiteMoves, blackMoves);
        }

        /// <summary>
        /// Extracts text from image bytes using LLM with a prompt that instructs about columns.
        /// </summary>
        /// <param name="imageBytes">The image bytes to process</param>
        /// <param name="language">The language of the chess notation</param>
        /// <param name="provider">The OCR provider to use: "gemini" (default) or "openai"</param>
        /// <returns>JSON string containing columns with moves</returns>
        public virtual async Task<string> ExtractTextFromImageAsyncWholeImage(byte[] imageBytes, string language, string provider = "gemini")
        {
            try
            {
                return provider switch
                {
                    "gemini" => await ExtractTextFromImageWithGeminiAsyncWholeImage(imageBytes, language),
                    "openai" => await ExtractTextFromImageWithOpenAIAsyncWholeImage(imageBytes, language),
                    _ => throw new ArgumentException($"Unsupported provider: {provider}. Supported providers are 'gemini' and 'openai'.", nameof(provider))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from whole image with provider {Provider}", provider);
                throw;
            }
        }

        /// <summary>
        /// Parses JSON response from LLM that contains columns with moves.
        /// Expected JSON format:
        /// {
        ///   "columns": [
        ///     { "columnIndex": 0, "moves": ["e4", "Nf3", ...] },
        ///     { "columnIndex": 1, "moves": ["e5", "Nc6", ...] },
        ///     ...
        ///   ]
        /// }
        /// </summary>
        private async Task<(List<string> whiteMoves, List<string> blackMoves)> ParseMovesFromColumnJsonAsync(string jsonResponse, string language)
        {
            var whiteMoves = new List<string>();
            var blackMoves = new List<string>();

            try
            {
                // Try to parse as JSON
                using var jsonDoc = JsonDocument.Parse(jsonResponse);
                var root = jsonDoc.RootElement;

                // Check if it has a "columns" property
                if (root.TryGetProperty("columns", out var columnsElement))
                {
                    foreach (var column in columnsElement.EnumerateArray())
                    {
                        if (!column.TryGetProperty("columnIndex", out var columnIndexElement) ||
                            !column.TryGetProperty("moves", out var movesElement))
                        {
                            _logger.LogWarning("Column missing required properties (columnIndex or moves)");
                            continue;
                        }

                        int columnIndex = columnIndexElement.GetInt32();
                        var moves = new List<string>();

                        foreach (var moveElement in movesElement.EnumerateArray())
                        {
                            var moveText = moveElement.GetString();
                            if (!string.IsNullOrWhiteSpace(moveText))
                            {
                                moves.Add(moveText.Trim());
                            }
                        }

                        // Process moves based on language
                        string[] processedMoves;
                        try
                        {
                            // Join moves into a JSON array string for processing
                            string movesJson = JsonSerializer.Serialize(moves.ToArray());
                            
                            if (language == "Greek")
                            {
                                _logger.LogInformation($"Processing Greek chess notation for column {columnIndex}");
                                string[] greekMoves = await _chessMoveProcessor.ProcessChessMovesAsync(movesJson);
                                processedMoves = await ConvertGreekMovesToEnglishAsync(greekMoves);
                            }
                            else
                            {
                                _logger.LogInformation($"Processing standard chess notation for column {columnIndex}");
                                processedMoves = await _chessMoveProcessor.ProcessChessMovesAsync(movesJson);
                            }

                            // Even columns (0, 2, 4...) are white moves, odd columns (1, 3, 5...) are black moves
                            if (columnIndex % 2 == 0)
                            {
                                whiteMoves.AddRange(processedMoves);
                            }
                            else
                            {
                                blackMoves.AddRange(processedMoves);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing chess moves for column {columnIndex} and language: {language}");
                            continue;
                        }
                    }
                }
                else
                {
                    // Fallback: try to parse as a simple array of moves
                    _logger.LogWarning("JSON response does not contain 'columns' property, attempting fallback parsing");
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        var allMoves = new List<string>();
                        foreach (var moveElement in root.EnumerateArray())
                        {
                            var moveText = moveElement.GetString();
                            if (!string.IsNullOrWhiteSpace(moveText))
                            {
                                allMoves.Add(moveText.Trim());
                            }
                        }
                        // If we can't determine columns, alternate between white and black
                        for (int i = 0; i < allMoves.Count; i++)
                        {
                            if (i % 2 == 0)
                            {
                                whiteMoves.Add(allMoves[i]);
                            }
                            else
                            {
                                blackMoves.Add(allMoves[i]);
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse JSON response from LLM. Response: {Response}", jsonResponse);
                throw new Exception("Failed to parse LLM response as JSON. The response may not be in the expected format.", ex);
            }

            _logger.LogInformation($"Extracted {whiteMoves.Count} white moves and {blackMoves.Count} black moves from whole image");
            return (whiteMoves, blackMoves);
        }

        /// <summary>
        /// Processes a chess image and returns the moves as a PGN string
        /// </summary>
        /// <param name="imagePath">Path to the chess image or URL</param>
        /// <param name="metadata">Optional metadata for PGN generation (player names, date, round)</param>
        /// <returns>PGN formatted string containing the chess moves</returns>
        public async Task<ChessGameResponse> ProcessImageAsync(string imagePath, PgnMetadata? metadata = null)
        {
            // Extract moves from the image using whole image processing (language is auto-detected)
            var (whiteMoves, blackMoves) = await ExtractMovesFromImageToStringAsync(imagePath);

            // Validate white and black moves separately
            var whiteValidation = _chessMoveValidator.ValidateMoves(whiteMoves.ToArray());
            var blackValidation = _chessMoveValidator.ValidateMoves(blackMoves.ToArray());

            // Validate moves in game context (alternating white and black)
            _chessMoveValidator.ValidateMovesInGameContext(whiteValidation, blackValidation);

            // Log validation results for white moves
            foreach (var move in whiteValidation.Moves)
            {
                switch (move.ValidationStatus)
                {
                    case "error":
                        _logger.LogError("White move validation error: Move {MoveNumber} '{Move}': {Error}",
                            move.MoveNumber, move.Notation, move.ValidationText);
                        break;
                    case "warning":
                        _logger.LogWarning("White move validation warning: Move {MoveNumber} '{Move}': {Warning}",
                            move.MoveNumber, move.Notation, move.ValidationText);
                        break;
                }
            }

            // Log validation results for black moves
            foreach (var move in blackValidation.Moves)
            {
                switch (move.ValidationStatus)
                {
                    case "error":
                        _logger.LogError("Black move validation error: Move {MoveNumber} '{Move}': {Error}",
                            move.MoveNumber, move.Notation, move.ValidationText);
                        break;
                    case "warning":
                        _logger.LogWarning("Black move validation warning: Move {MoveNumber} '{Move}': {Warning}",
                            move.MoveNumber, move.Notation, move.ValidationText);
                        break;
                }
            }

            // Construct ChessMovePair list and extract validated moves for PGN
            var validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            };
            
            // Build lists of validated moves (using NormalizedNotation which contains corrections)
            var validatedWhiteMoves = new List<string>();
            var validatedBlackMoves = new List<string>();
            
            int maxMoves = Math.Max(whiteValidation.Moves.Count, blackValidation.Moves.Count);
            for (int i = 0; i < maxMoves; i++)
            {
                var movePair = new ChessMovePair
                {
                    MoveNumber = i + 1,
                    WhiteMove = (i < whiteValidation.Moves.Count) ? new Models.ValidatedMove {
                        Notation = whiteValidation.Moves[i].Notation,
                        NormalizedNotation = whiteValidation.Moves[i].NormalizedNotation,
                        ValidationStatus = whiteValidation.Moves[i].ValidationStatus,
                        ValidationText = whiteValidation.Moves[i].ValidationText
                    } : null,
                    BlackMove = (i < blackValidation.Moves.Count) ? new Models.ValidatedMove {
                        Notation = blackValidation.Moves[i].Notation,
                        NormalizedNotation = blackValidation.Moves[i].NormalizedNotation,
                        ValidationStatus = blackValidation.Moves[i].ValidationStatus,
                        ValidationText = blackValidation.Moves[i].ValidationText
                    } : null
                };
                validation.Moves.Add(movePair);
                
                // Collect validated moves for PGN (use NormalizedNotation which has corrections)
                if (i < whiteValidation.Moves.Count && !string.IsNullOrWhiteSpace(whiteValidation.Moves[i].NormalizedNotation))
                {
                    validatedWhiteMoves.Add(whiteValidation.Moves[i].NormalizedNotation!);
                }
                if (i < blackValidation.Moves.Count && !string.IsNullOrWhiteSpace(blackValidation.Moves[i].NormalizedNotation))
                {
                    validatedBlackMoves.Add(blackValidation.Moves[i].NormalizedNotation!);
                }
            }

            // Generate the PGN content using validated moves (with corrections applied)
            var pgnContent = GeneratePGNContentAsync(validatedWhiteMoves, validatedBlackMoves, metadata);

            return new ChessGameResponse
            {
                PgnContent = pgnContent,
                Validation = validation
            };
        }

        protected virtual async Task<byte[]> LoadAndProcessImageAsync(string imagePath)
        {
            // Load the image
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);

            // Resize the image if necessary
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1024, 1024),
                Mode = ResizeMode.Max
            }));

            // Convert the image to bytes
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await image.SaveAsJpegAsync(ms);
                imageBytes = ms.ToArray();
            }
            return imageBytes;
        }

        public virtual async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language, string provider = "gemini")
        {
            try
            {
                provider = provider?.ToLowerInvariant() ?? "gemini";
                
                return provider switch
                {
                    "gemini" => await ExtractTextFromImageWithGeminiAsync(imageBytes, language),
                    "openai" => await ExtractTextFromImageWithOpenAIAsync(imageBytes, language),
                    _ => throw new ArgumentException($"Unsupported provider: {provider}. Supported providers are 'gemini' and 'openai'.", nameof(provider))
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image with provider {Provider}", provider);
                throw;
            }
        }

        private async Task<string> ExtractTextFromImageWithGeminiAsync(byte[] imageBytes, string language)
        {
            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? 
                _configuration["GEMINI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("GEMINI_API_KEY environment variable not set");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            
            var base64Image = Convert.ToBase64String(imageBytes);

            // Get valid characters for the specified language
            var validChars = GetChessNotationCharacters(language);

            // Build the prompt with the valid characters
            var promptText = "You are an OCR engine. Transcribe all visible chess moves from this image exactly as they appear, but only include characters that are valid in a chess game.";
            promptText += $"The characters are written in {language}, valid characters are: ";
            
            // Add each valid character to the prompt
            for (int i = 0; i < validChars.Length; i++)
            {
                if (i > 0)
                {
                    promptText += ", ";
                }
                promptText += validChars[i];
            }
            
            promptText += ". Do not include any other characters, and preserve any misspellings or punctuation errors. \n Return the raw text as a json list having all the moves.";

            var requestData = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                text = promptText
                            },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 4000,
                    thinking_config = new
                    {
                        thinking_level = "low"
                    },
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-preview:generateContent", content);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Invalid API key");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            
            if (!jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                throw new Exception("Gemini API returned no candidates in response");
            }
            
            var firstCandidate = candidates[0];
            
            // Check for finish reason (e.g., MAX_TOKENS indicates response was truncated)
            if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
            {
                var finishReasonValue = finishReason.GetString();
                if (finishReasonValue == "MAX_TOKENS")
                {
                    _logger.LogWarning("Gemini API response was truncated due to MAX_TOKENS limit. Consider increasing maxOutputTokens.");
                    throw new Exception("Gemini API response was truncated because it exceeded the maximum token limit. The response may be incomplete. Consider increasing maxOutputTokens in the request.");
                }
            }
            
            // Check if content exists and is not empty
            if (!firstCandidate.TryGetProperty("content", out var contentElement) || contentElement.ValueKind == JsonValueKind.Null)
            {
                throw new Exception("Gemini API returned empty content in response");
            }
            
            // Check if parts array exists
            if (!contentElement.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            {
                throw new Exception("Gemini API returned no parts in response");
            }
            
            // Extract text from the first part
            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textElement))
            {
                throw new Exception("Gemini API response part does not contain 'text' property");
            }
            
            var text = textElement.GetString();
            
            // Optionally log usage metadata if available
            if (jsonDoc.RootElement.TryGetProperty("usageMetadata", out var usageMetadata))
            {
                if (usageMetadata.TryGetProperty("totalTokenCount", out var totalTokens))
                {
                    _logger.LogDebug("Gemini API usage: {TotalTokens} total tokens", totalTokens.GetInt32());
                }
            }
            
            return text ?? string.Empty;
        }

        private async Task<string> ExtractTextFromImageWithGeminiAsyncWholeImage(byte[] imageBytes, string language)
        {
            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? 
                _configuration["GEMINI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("GEMINI_API_KEY environment variable not set");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            
            var base64Image = Convert.ToBase64String(imageBytes);

            // Get valid characters for the specified language
            var validChars = GetChessNotationCharacters(language);

            // Build the prompt with column-aware instructions
            var promptText = "You are an OCR engine specialized in chess notation. This image contains a chess game written in columns. ";
            promptText += "The image may have 2, 4, or 6 columns. ";
            promptText += "Columns alternate between white moves (even-numbered columns: 0, 2, 4...) and black moves (odd-numbered columns: 1, 3, 5...). ";
            promptText += "Each column contains multiple chess moves written vertically. ";
            promptText += "Analyze the image to determine the number of columns automatically. ";
            promptText += $"The characters are written in {language}, valid characters are: ";
            
            // Add each valid character to the prompt
            for (int i = 0; i < validChars.Length; i++)
            {
                if (i > 0)
                {
                    promptText += ", ";
                }
                promptText += validChars[i];
            }
            
            promptText += ". Do not include any other characters, and preserve any misspellings or punctuation errors.\n\n";
            promptText += "Return the result as a JSON object with the following structure:\n";
            promptText += "{\n";
            promptText += "  \"columns\": [\n";
            promptText += "    { \"columnIndex\": 0, \"moves\": [\"e4\", \"Nf3\", ...] },\n";
            promptText += "    { \"columnIndex\": 1, \"moves\": [\"e5\", \"Nc6\", ...] },\n";
            promptText += "    ...\n";
            promptText += "  ]\n";
            promptText += "}\n\n";
            promptText += "Important: Extract moves from left to right, column by column. Even column indices (0, 2, 4...) contain white moves, odd column indices (1, 3, 5...) contain black moves.";

            var requestData = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                text = promptText
                            },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 8000,
                    response_mime_type = "application/json",
                    thinking_config = new
                    {
                        thinking_level = "low"
                    },
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-preview:generateContent", content);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Invalid API key");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            
            if (!jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                throw new Exception("Gemini API returned no candidates in response");
            }
            
            var firstCandidate = candidates[0];
            
            // Check for finish reason
            if (firstCandidate.TryGetProperty("finishReason", out var finishReason))
            {
                var finishReasonValue = finishReason.GetString();
                if (finishReasonValue == "MAX_TOKENS")
                {
                    _logger.LogWarning("Gemini API response was truncated due to MAX_TOKENS limit. Consider increasing maxOutputTokens.");
                    throw new Exception("Gemini API response was truncated because it exceeded the maximum token limit. The response may be incomplete. Consider increasing maxOutputTokens in the request.");
                }
            }
            
            // Check if content exists and is not empty
            if (!firstCandidate.TryGetProperty("content", out var contentElement) || contentElement.ValueKind == JsonValueKind.Null)
            {
                throw new Exception("Gemini API returned empty content in response");
            }
            
            // Check if parts array exists
            if (!contentElement.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
            {
                throw new Exception("Gemini API returned no parts in response");
            }
            
            // Extract text from the first part
            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textElement))
            {
                throw new Exception("Gemini API response part does not contain 'text' property");
            }
            
            var text = textElement.GetString();
            
            // Optionally log usage metadata if available
            if (jsonDoc.RootElement.TryGetProperty("usageMetadata", out var usageMetadata))
            {
                if (usageMetadata.TryGetProperty("totalTokenCount", out var totalTokens))
                {
                    _logger.LogDebug("Gemini API usage: {TotalTokens} total tokens", totalTokens.GetInt32());
                }
            }
            
            return text ?? string.Empty;
        }

        private async Task<string> ExtractTextFromImageWithOpenAIAsyncWholeImage(byte[] imageBytes, string language)
        {
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
                _configuration["OPENAI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("OPENAI_API_KEY environment variable not set");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            var base64Image = Convert.ToBase64String(imageBytes);

            // Get valid characters for the specified language
            var validChars = GetChessNotationCharacters(language);

            // Build the prompt with column-aware instructions
            var promptText = "You are an OCR engine specialized in chess notation. This image contains a chess game written in columns. ";
            promptText += "The image may have 2, 4, or 6 columns. ";
            promptText += "Columns alternate between white moves (even-numbered columns: 0, 2, 4...) and black moves (odd-numbered columns: 1, 3, 5...). ";
            promptText += "Each column contains multiple chess moves written vertically. ";
            promptText += "Analyze the image to determine the number of columns automatically. ";
            promptText += $"The characters are written in {language}, valid characters are: ";
            
            // Add each valid character to the prompt
            for (int i = 0; i < validChars.Length; i++)
            {
                if (i > 0)
                {
                    promptText += ", ";
                }
                promptText += validChars[i];
            }
            
            promptText += ". Do not include any other characters, and preserve any misspellings or punctuation errors.\n\n";
            promptText += "Return the result as a JSON object with the following structure:\n";
            promptText += "{\n";
            promptText += "  \"columns\": [\n";
            promptText += "    { \"columnIndex\": 0, \"moves\": [\"e4\", \"Nf3\", ...] },\n";
            promptText += "    { \"columnIndex\": 1, \"moves\": [\"e5\", \"Nc6\", ...] },\n";
            promptText += "    ...\n";
            promptText += "  ]\n";
            promptText += "}\n\n";
            promptText += "Important: Extract moves from left to right, column by column. Even column indices (0, 2, 4...) contain white moves, odd column indices (1, 3, 5...) contain black moves.";

            var requestData = new
            {
                model = "gpt-5-chat-latest",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = promptText
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{base64Image}"
                                }
                            }
                        }
                    }
                },
                max_tokens = 4000,
                response_format = new { type = "json_object" }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Invalid API key");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            
            if (!jsonDoc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                throw new Exception("OpenAI API returned no choices in response");
            }
            
            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message))
            {
                throw new Exception("OpenAI API response choice does not contain 'message' property");
            }
            
            if (!message.TryGetProperty("content", out var contentElement))
            {
                throw new Exception("OpenAI API response message does not contain 'content' property");
            }
            
            var text = contentElement.GetString();
            
            // Optionally log usage metadata if available
            if (jsonDoc.RootElement.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("total_tokens", out var totalTokens))
                {
                    _logger.LogDebug("OpenAI API usage: {TotalTokens} total tokens", totalTokens.GetInt32());
                }
            }
            
            return text ?? string.Empty;
        }

        private async Task<string> ExtractTextFromImageWithOpenAIAsync(byte[] imageBytes, string language)
        {
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
                _configuration["OPENAI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("OPENAI_API_KEY environment variable not set");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            var base64Image = Convert.ToBase64String(imageBytes);

            // Get valid characters for the specified language
            var validChars = GetChessNotationCharacters(language);

            // Build the prompt with the valid characters
            var promptText = "You are an OCR engine. Transcribe all visible chess moves from this image exactly as they appear, but only include characters that are valid in a chess game.";
            promptText += $"The characters are written in {language}, valid characters are: ";
            
            // Add each valid character to the prompt
            for (int i = 0; i < validChars.Length; i++)
            {
                if (i > 0)
                {
                    promptText += ", ";
                }
                promptText += validChars[i];
            }
            
            promptText += ". Do not include any other characters, and preserve any misspellings or punctuation errors. \n Return the raw text as a json list having all the moves.";

            var requestData = new
            {
                model = "gpt-5-chat-latest",
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = promptText
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{base64Image}"
                                }
                            }
                        }
                    }
                },
                max_tokens = 1000
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Invalid API key");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            
            var choices = jsonDoc.RootElement.GetProperty("choices");
            var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
            
            return messageContent ?? string.Empty;
        }

        private Task<string[]> ConvertGreekMovesToEnglishAsync(string[] greekMoves)
        {
            var englishMoves = new string[greekMoves.Length];

            for (int i = 0; i < greekMoves.Length; i++)
            {
                string englishMove = greekMoves[i];

                // Replace Greek piece names and file letters with English equivalents
                foreach (var (greek, english) in GreekToEnglishMap)
                {
                    englishMove = englishMove.Replace(greek, english);
                }

                // Handle special cases
                englishMove = HandleSpecialCases(englishMove);

                englishMoves[i] = englishMove;
            }

            return Task.FromResult(englishMoves);
        }

        private string HandleSpecialCases(string move)
        {
            // Handle castling notation
            move = move.Replace("0-0", "O-O");  // Kingside castling
            move = move.Replace("0-0-0", "O-O-O");  // Queenside castling

            // Handle pawn promotion if present
            if (move.Contains("="))
            {
                // Ensure the promotion piece is in English notation
                foreach (var (greek, english) in GreekToEnglishMap)
                {
                    move = move.Replace($"={greek}", $"={english}");
                }
            }

            return move;
        }

        /// <summary>
        /// Returns a list of valid characters in chess notation for the specified language.
        /// </summary>
        /// <param name="language">The language of chess notation (e.g., "Greek", "English")</param>
        /// <returns>An array of valid characters for the specified language</returns>
        private string[] GetChessNotationCharacters(string language)
        {
            return language switch
            {
                "Greek" => new[]
                {
                    "Π", "Α", "Β", "Ι", "Ρ", "A", "B", "I", "P", // Greek piece names
                    "0", "O", "χ", "Ο", "x", "О", "о", "+", "#", "=", // Special symbols
                    "α", "β", "γ", "δ", "ε", "ζ", "η", "θ", // Greek file letters
                    "1", "2", "3", "4", "5", "6", "7", "8", // Rank numbers
                },
                "English" => new[]
                {
                    "R", "N", "B", "Q", "K", // English piece names
                    "0", "Ο", "x", "+", "#", "=", // Special symbols
                    "a", "b", "c", "d", "e", "f", "g", "h", // File letters
                    "1", "2", "3", "4", "5", "6", "7", "8", // Rank numbers
                },
                _ => Array.Empty<string>() // Return empty array for unsupported languages
            };
        }

        public string GeneratePGNContentAsync(IEnumerable<string> whiteMoves, IEnumerable<string> blackMoves, PgnMetadata? metadata = null)
        {
            // Basic PGN structure
            var sb = new StringBuilder();
            //sb.AppendLine("[Event \"??\"]");
            //sb.AppendLine("[Site \"??\"]");
            
            // Date
            if (metadata?.GameDate.HasValue == true)
            {
                sb.AppendLine($"[Date \"{metadata.GameDate.Value:yyyy.MM.dd}\"]");
            }
            else
            {
                sb.AppendLine("[Date \"????.??.??\"]");
            }
            
            // Round (optional)
            if (!string.IsNullOrWhiteSpace(metadata?.Round))
            {
                sb.AppendLine($"[Round \"{metadata.Round}\"]");
            }
            
            // White player
            var whitePlayer = string.IsNullOrWhiteSpace(metadata?.WhitePlayer) ? "?" : metadata.WhitePlayer;
            sb.AppendLine($"[White \"{whitePlayer}\"]");
            
            // Black player
            var blackPlayer = string.IsNullOrWhiteSpace(metadata?.BlackPlayer) ? "?" : metadata.BlackPlayer;
            sb.AppendLine($"[Black \"{blackPlayer}\"]");
            
            sb.AppendLine("[Result \"*\"]");
            sb.AppendLine();

            var whiteList = whiteMoves?.ToList() ?? new List<string>();
            var blackList = blackMoves?.ToList() ?? new List<string>();
            int maxMoves = Math.Max(whiteList.Count, blackList.Count);
            var moveList = new List<string>();
            for (int i = 0; i < maxMoves; i++)
            {
                string white = i < whiteList.Count ? whiteList[i] ?? string.Empty : string.Empty;
                string black = i < blackList.Count ? blackList[i] ?? string.Empty : string.Empty;
                if (!string.IsNullOrWhiteSpace(white) || !string.IsNullOrWhiteSpace(black))
                {
                    if (!string.IsNullOrWhiteSpace(white))
                        moveList.Add($"{i + 1}. {white}");
                    if (!string.IsNullOrWhiteSpace(black))
                    {
                        moveList.Add(black);
                        moveList.Add("\n");
                    }
                }
            }
            sb.AppendLine(string.Join(" ", moveList) + " *");
            return sb.ToString();
        }

        public async Task<string> DebugUploadAsync(string imagePath, string promptText)
        {
            // Check if file exists
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found", imagePath);
            }

            // Load and process the image
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imagePath);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(1024, 1024),
                Mode = ResizeMode.Max
            }));

            // Convert the image to bytes
            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await image.SaveAsJpegAsync(ms);
                imageBytes = ms.ToArray();
            }

            string apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? 
                _configuration["GEMINI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("GEMINI_API_KEY environment variable not set");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            
            var base64Image = Convert.ToBase64String(imageBytes);

            var requestData = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                text = promptText
                            },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = "image/jpeg",
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = 1000
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent", content);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    throw new UnauthorizedAccessException("Invalid API key");
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API error: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseContent);
            
            if (!jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                throw new Exception("Gemini API returned no candidates in response");
            }
            
            var contentElement = candidates[0].GetProperty("content");
            var parts = contentElement.GetProperty("parts");
            if (parts.GetArrayLength() == 0)
            {
                throw new Exception("Gemini API returned no parts in response");
            }
            
            var text = parts[0].GetProperty("text").GetString();
            
            return text?.Replace("`", "") ?? string.Empty;
        }

        /// <summary>
        /// Splits the input image into vertical columns based on projection profile and returns the column boundaries.
        /// </summary>
        /// <param name="image">The chess moves image</param>
        /// <param name="expectedColumns">Expected number of columns (default: 6)</param>
        /// <returns>List of column boundary indices (pixel positions)</returns>
        public List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns = 6)
        {
            return SplitImageIntoColumns(image, expectedColumns, null);
        }

        /// <summary>
        /// Automatically detects chess move columns by analyzing width patterns and content distribution.
        /// Finds 6 consecutive columns of similar width that likely contain chess moves.
        /// </summary>
        /// <param name="image">The chess moves image</param>
        /// <param name="searchRegion">Rectangle to search within (null for entire image)</param>
        /// <param name="useHeuristics">Whether to use heuristics for detection or fall back to equal division immediately</param>
        /// <returns>List of column boundary indices for the 6 chess move columns</returns>
        public List<int> DetectChessColumnsAutomatically(Image<Rgba32> image, Rectangle? searchRegion = null, bool useHeuristics = true, int expectedColumns = 4)
        {
            Rectangle region = searchRegion ?? new Rectangle(0, 0, image.Width, image.Height);
            
            // If heuristics are disabled, immediately use equal division
            if (!useHeuristics)
            {
                _logger.LogInformation($"Column detection heuristics disabled, using equal division for region: {region.Width}x{region.Height} with expectedColumns: {expectedColumns}");
                return CreateEqualDivisionColumns(region, expectedColumns);
            }
            
            _logger.LogInformation($"Starting automatic chess column detection within region: {region.Width}x{region.Height} with expectedColumns: {expectedColumns}");
            
            // Calculate minimum column width based on table size (each column should be ~1/expectedColumns of table, with some tolerance)
            double minExpectedWidth = region.Width / (double)expectedColumns * 0.5; // At least 50% of expected width
            double maxExpectedWidth = region.Width / (double)expectedColumns * 2.0; // At most 200% of expected width
            
            _logger.LogInformation($"Expected column width range: {minExpectedWidth:F1}px - {maxExpectedWidth:F1}px");

            // Step 1: Detect significant column boundaries
            var allBoundaries = DetectAllColumnBoundaries(image, region);
            _logger.LogInformation($"Detected {allBoundaries.Count - 1} significant column boundaries");

            if (allBoundaries.Count < 4) // Need at least 4 boundaries for 3 columns (minimum)
            {
                _logger.LogWarning("Not enough column boundaries detected, falling back to equal division with expectedColumns: {ExpectedColumns}", expectedColumns);
                return CreateEqualDivisionColumns(region, expectedColumns);
            }

            // Step 2: Analyze column widths and filter out obviously inappropriate ones
            var columnData = AnalyzeColumnWidths(allBoundaries, region);
            var reasonableColumns = columnData.Where(c => c.Width >= minExpectedWidth * 0.3).ToList(); // Very lenient for initial filtering
            
            _logger.LogInformation($"Filtered to {reasonableColumns.Count} columns with reasonable widths");
            
            // Step 3: Find the best sequence of expectedColumns consecutive columns that cover most of the table
            var chessColumns = FindChessColumnSequence(reasonableColumns, region, expectedColumns);
            
            if (chessColumns != null)
            {
                double totalWidth = chessColumns[expectedColumns] - chessColumns[0];
                double avgWidth = totalWidth / (double)expectedColumns;
                double coverage = totalWidth / region.Width;
                _logger.LogInformation($"Found chess columns covering {coverage:P1} of table width, avg column width: {avgWidth:F1}px");
                return chessColumns;
            }
            
            _logger.LogWarning("Could not automatically detect chess columns, falling back to equal division with expectedColumns: {ExpectedColumns}", expectedColumns);
            return CreateEqualDivisionColumns(region, expectedColumns);
        }

        /// <summary>
        /// Detects all column boundaries with high sensitivity, then filters appropriately.
        /// </summary>
        private List<int> DetectAllColumnBoundaries(Image<Rgba32> image, Rectangle region)
        {
            image.Mutate(x => x.Grayscale());
            int regionWidth = region.Width;
            int regionHeight = region.Height;
            double[] columnSums = new double[regionWidth];
            
            // Calculate column projection profile
            for (int relativeX = 0; relativeX < regionWidth; relativeX++)
            {
                int absoluteX = region.X + relativeX;
                if (absoluteX >= image.Width) break;
                
                double sum = 0;
                for (int relativeY = 0; relativeY < regionHeight; relativeY++)
                {
                    int absoluteY = region.Y + relativeY;
                    if (absoluteY >= image.Height) break;
                    
                    var pixel = image[absoluteX, absoluteY];
                    int gray = pixel.R;
                    int binary = gray > 128 ? 1 : 0;
                    sum += binary;
                }
                columnSums[relativeX] = sum;
            }
            
            // Light smoothing to preserve boundaries but reduce noise
            int smoothWindow = Math.Max(2, regionWidth / 200);
            double[] smoothed = SmoothProfile(columnSums, smoothWindow);
            
            // Log projection profile statistics for debugging
            double minValue = smoothed.Min();
            double maxValue = smoothed.Max();
            double avgValue = smoothed.Average();
            _logger.LogInformation($"Projection profile - Min: {minValue:F2}, Max: {maxValue:F2}, Avg: {avgValue:F2}, Range: {maxValue - minValue:F2}");
            
            // Use multiple detection methods for better coverage
            List<int> allBoundaries = new List<int> { 0 };
            
            // Method 1: Valley detection (original approach but more sensitive)
            var valleyBoundaries = DetectValleys(smoothed, regionWidth);
            _logger.LogInformation($"Valley detection found {valleyBoundaries.Count} boundaries: [{string.Join(", ", valleyBoundaries)}]");
            allBoundaries.AddRange(valleyBoundaries);
            
            // Method 2: Gradient-based detection for sharp transitions
            var gradientBoundaries = DetectGradientBoundaries(smoothed, regionWidth);
            _logger.LogInformation($"Gradient detection found {gradientBoundaries.Count} boundaries: [{string.Join(", ", gradientBoundaries)}]");
            allBoundaries.AddRange(gradientBoundaries);
            
            // Method 3: Local minima detection
            var minimaBoundaries = DetectLocalMinima(smoothed, regionWidth);
            _logger.LogInformation($"Local minima detection found {minimaBoundaries.Count} boundaries: [{string.Join(", ", minimaBoundaries)}]");
            allBoundaries.AddRange(minimaBoundaries);
            
            allBoundaries.Add(regionWidth);
            
            // Remove duplicates and sort
            allBoundaries = allBoundaries.Distinct().OrderBy(b => b).ToList();
            
            // Now apply intelligent filtering - remove boundaries that are too close together
            List<int> filteredBoundaries = new List<int> { allBoundaries[0] };
            int minSeparation = Math.Max(3, regionWidth / 100); // Minimum 1% separation or 3px
            
            for (int i = 1; i < allBoundaries.Count; i++)
            {
                int separation = allBoundaries[i] - filteredBoundaries.Last();
                if (separation >= minSeparation || i == allBoundaries.Count - 1) // Always keep the last boundary
                {
                    filteredBoundaries.Add(allBoundaries[i]);
                }
            }
            
            // Convert to absolute coordinates
            for (int i = 0; i < filteredBoundaries.Count; i++)
            {
                filteredBoundaries[i] += region.X;
            }
            
            _logger.LogInformation($"Detected {filteredBoundaries.Count - 1} column boundaries using multi-method approach (min separation: {minSeparation}px)");
            return filteredBoundaries;
        }

        /// <summary>
        /// Detects valleys in the projection profile.
        /// </summary>
        private List<int> DetectValleys(double[] smoothed, int width)
        {
            var boundaries = new List<int>();
            double avgValue = smoothed.Average();
            double threshold = Math.Max(1.0, avgValue * 0.05); // Very low threshold
            
            for (int x = 1; x < width - 1; x++)
            {
                double diff = Math.Abs(smoothed[x] - smoothed[x - 1]) + Math.Abs(smoothed[x] - smoothed[x + 1]);
                bool isValley = smoothed[x] < smoothed[x - 1] && smoothed[x] < smoothed[x + 1];
                if (diff > threshold && isValley)
                {
                    boundaries.Add(x);
                }
            }
            
            return boundaries;
        }

        /// <summary>
        /// Detects boundaries based on gradient analysis.
        /// </summary>
        private List<int> DetectGradientBoundaries(double[] smoothed, int width)
        {
            var boundaries = new List<int>();
            double[] gradient = new double[width - 1];
            
            // Calculate gradient
            for (int i = 0; i < width - 1; i++)
            {
                gradient[i] = smoothed[i + 1] - smoothed[i];
            }
            
            // Find zero crossings in gradient (transition points)
            for (int i = 1; i < gradient.Length - 1; i++)
            {
                if ((gradient[i - 1] > 0 && gradient[i] < 0) || (gradient[i - 1] < 0 && gradient[i] > 0))
                {
                    double magnitude = Math.Abs(gradient[i - 1]) + Math.Abs(gradient[i]);
                    if (magnitude > smoothed.Average() * 0.02) // Threshold for significant transitions
                    {
                        boundaries.Add(i);
                    }
                }
            }
            
            return boundaries;
        }

        /// <summary>
        /// Detects local minima in the projection profile.
        /// </summary>
        private List<int> DetectLocalMinima(double[] smoothed, int width)
        {
            var boundaries = new List<int>();
            int windowSize = Math.Max(3, width / 100);
            
            for (int i = windowSize; i < width - windowSize; i++)
            {
                bool isMinimum = true;
                double centerValue = smoothed[i];
                
                // Check if this point is lower than all points in the window
                for (int j = i - windowSize; j <= i + windowSize; j++)
                {
                    if (j != i && smoothed[j] <= centerValue)
                    {
                        isMinimum = false;
                        break;
                    }
                }
                
                if (isMinimum && centerValue < smoothed.Average() * 0.8) // Below average
                {
                    boundaries.Add(i);
                }
            }
            
            return boundaries;
        }

        /// <summary>
        /// Analyzes column widths to identify patterns.
        /// </summary>
        private List<ColumnInfo> AnalyzeColumnWidths(List<int> boundaries, Rectangle region)
        {
            var columns = new List<ColumnInfo>();
            
            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                int width = boundaries[i + 1] - boundaries[i];
                columns.Add(new ColumnInfo
                {
                    Index = i,
                    StartX = boundaries[i],
                    EndX = boundaries[i + 1],
                    Width = width,
                    RelativeWidth = (double)width / region.Width
                });
            }
            
            _logger.LogInformation($"Column width analysis: {string.Join(", ", columns.Select(c => $"{c.Width}px"))}");
            return columns;
        }

        /// <summary>
        /// Finds the best sequence of 6 consecutive columns that likely contain chess moves.
        /// Detects and skips comment columns (significantly wider than chess columns).
        /// </summary>
        private List<int>? FindChessColumnSequence(List<ColumnInfo> columns, Rectangle region, int expectedColumns = 4)
        {
            if (columns.Count < 3) 
            {
                _logger.LogWarning($"Not enough columns ({columns.Count}) to attempt chess column detection (need at least 3)");
                return null;
            }
            
            // Step 1: Identify and exclude outlier columns (comments, numbers, etc.)
            var filteredColumns = FilterOutlierColumns(columns, region, expectedColumns);
            _logger.LogInformation($"After outlier filtering: {filteredColumns.Count} columns remaining");
            
            if (filteredColumns.Count < 3)
            {
                _logger.LogInformation("Too few columns after outlier filtering, using original set");
                filteredColumns = columns; // Fallback to original set
            }
            else if (filteredColumns.Count < expectedColumns)
            {
                _logger.LogInformation($"Only {filteredColumns.Count} columns after filtering, but proceeding to see what we can detect");
                // Continue with filtered set even if < expectedColumns columns
            }
            
            var candidates = new List<ColumnSequence>();
            
            // Determine how many columns to look for (prefer expectedColumns, but work with what we have)
            int targetColumns = Math.Min(expectedColumns, filteredColumns.Count);
            _logger.LogInformation($"Looking for sequences of {targetColumns} columns");
            
            // Find all possible sequences of target number of consecutive columns
            for (int start = 0; start <= filteredColumns.Count - targetColumns; start++)
            {
                var sequence = filteredColumns.Skip(start).Take(targetColumns).ToList();
                
                // Verify this is actually a consecutive sequence (no major gaps in column indices)
                bool isReasonablyConsecutive = IsReasonablyConsecutive(sequence);
                
                if (!isReasonablyConsecutive) continue;
                
                var candidate = AnalyzeColumnSequence(sequence, region);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
            
            // If no sequences found with filtered columns, try with original set
            if (!candidates.Any() && filteredColumns != columns)
            {
                _logger.LogInformation("No sequences found with filtered columns, trying original set");
                int originalTargetColumns = Math.Min(expectedColumns, columns.Count);
                for (int start = 0; start <= columns.Count - originalTargetColumns; start++)
                {
                    var sequence = columns.Skip(start).Take(originalTargetColumns).ToList();
                    var candidate = AnalyzeColumnSequence(sequence, region);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }
            
            if (!candidates.Any()) 
            {
                _logger.LogWarning("No valid column sequences found");
                return null;
            }
            
            // Select the best candidate based on multiple criteria, preferring those without outliers
            var bestCandidate = candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.WidthUniformity) // Prefer uniform widths
                .First();
                
            double coverage = (bestCandidate.EndX - bestCandidate.StartX) / (double)region.Width;
            _logger.LogInformation($"Selected column sequence with score {bestCandidate.Score:F2}, avg width {bestCandidate.AverageWidth:F1}px, coverage {coverage:P1}");
            
            // Return the boundaries for the selected sequence
            var result = new List<int> { bestCandidate.StartX };
            result.AddRange(bestCandidate.Columns.Select(c => c.EndX));
            
            // If we have fewer than expectedColumns columns but good quality, try to extrapolate to expectedColumns
            if (bestCandidate.Columns.Count < expectedColumns && bestCandidate.Score > 0.7)
            {
                result = ExtrapolateToNColumns(result, bestCandidate, region, expectedColumns);
            }
            else if (bestCandidate.Columns.Count < expectedColumns)
            {
                _logger.LogWarning($"Found only {bestCandidate.Columns.Count} columns with low score {bestCandidate.Score:F2}, falling back to equal division");
                return CreateEqualDivisionColumns(region, expectedColumns);
            }
            
            return result;
        }

        /// <summary>
        /// Extrapolates a good column sequence to exactly expectedColumns columns.
        /// </summary>
        private List<int> ExtrapolateToNColumns(List<int> boundaries, ColumnSequence sequence, Rectangle region, int expectedColumns)
        {
            int currentColumns = sequence.Columns.Count;
            int neededColumns = expectedColumns - currentColumns;
            
            _logger.LogInformation($"Extrapolating {currentColumns} columns to {expectedColumns} columns (need {neededColumns} more)");
            
            if (neededColumns <= 0) return boundaries;
            
            double avgWidth = sequence.AverageWidth;
            var result = new List<int>(boundaries);
            
            // Strategy: Add columns at the end by extending with average width
            int lastBoundary = result.Last();
            for (int i = 0; i < neededColumns; i++)
            {
                int newBoundary = lastBoundary + (int)avgWidth;
                // Make sure we don't exceed the region
                newBoundary = Math.Min(newBoundary, region.X + region.Width);
                result.Add(newBoundary);
                lastBoundary = newBoundary;
            }
            
            _logger.LogInformation($"Extrapolated to {expectedColumns} columns: added {neededColumns} columns with avg width {avgWidth:F1}px");
            return result;
        }

        /// <summary>
        /// Filters out columns that are likely comments or numbers (significantly different widths).
        /// </summary>
        private List<ColumnInfo> FilterOutlierColumns(List<ColumnInfo> columns, Rectangle region, int expectedColumns = 4)
        {
            if (columns.Count < 3) return columns;
            
            // Calculate statistics
            var widths = columns.Select(c => c.Width).ToList();
            double medianWidth = widths.OrderBy(w => w).Skip(widths.Count / 2).First();
            double avgWidth = widths.Average();
            double expectedChessWidth = region.Width / (double)expectedColumns; // Expected chess column width
            
            _logger.LogInformation($"Column width analysis - Median: {medianWidth:F1}px, Average: {avgWidth:F1}px, Expected chess: {expectedChessWidth:F1}px");
            
            // Identify outliers using statistical analysis
            var filtered = new List<ColumnInfo>();
            
            foreach (var column in columns)
            {
                double deviationFromMedian = Math.Abs(column.Width - medianWidth) / medianWidth;
                double deviationFromExpected = Math.Abs(column.Width - expectedChessWidth) / expectedChessWidth;
                
                // Consider a column an outlier if:
                // 1. It's more than 50% different from the median width, OR
                // 2. It's more than 80% different from expected chess column width, OR  
                // 3. It's the first column and significantly larger (common comment pattern)
                bool isOutlier = deviationFromMedian > 0.5 || 
                                deviationFromExpected > 0.8 ||
                                (column.Index == 0 && column.Width > medianWidth * 1.4);
                
                if (isOutlier)
                {
                    _logger.LogInformation($"Column {column.Index} (width {column.Width}px) identified as outlier - deviation from median: {deviationFromMedian:P1}");
                }
                else
                {
                    filtered.Add(column);
                }
            }
            
            return filtered;
        }

        /// <summary>
        /// Checks if a sequence of columns is reasonably consecutive (allowing small gaps).
        /// </summary>
        private bool IsReasonablyConsecutive(List<ColumnInfo> sequence)
        {
            if (sequence.Count < 2) return true;
            
            // Allow gaps of up to 2 indices (in case we filtered out some columns)
            for (int i = 1; i < sequence.Count; i++)
            {
                int gap = sequence[i].Index - sequence[i-1].Index;
                if (gap > 3) // Too large a gap
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Analyzes a sequence of columns to determine if they're suitable for chess moves.
        /// Ensures columns occupy 80-100% of table width with uniform widths.
        /// Applies strict uniformity checks to reject wildly inconsistent widths.
        /// </summary>
        private ColumnSequence? AnalyzeColumnSequence(List<ColumnInfo> sequence, Rectangle region)
        {
            if (sequence.Count < 3) return null; // Need at least 3 columns to analyze
            
            int sequenceCount = sequence.Count;
            _logger.LogInformation($"Analyzing sequence of {sequenceCount} columns");
            
            var widths = sequence.Select(c => c.Width).ToArray();
            double avgWidth = widths.Average();
            double minWidth = widths.Min();
            double maxWidth = widths.Max();
            double widthRange = maxWidth - minWidth;
            double widthVariance = widths.Select(w => Math.Pow(w - avgWidth, 2)).Average();
            double standardDeviation = Math.Sqrt(widthVariance);
            
            // Calculate width uniformity metrics
            double coefficientOfVariation = standardDeviation / avgWidth; // Lower is better
            double widthRangeRatio = widthRange / avgWidth; // Lower is better
            double minMaxRatio = minWidth / maxWidth; // Higher is better (closer to 1)
            
            _logger.LogInformation($"Width analysis: min={minWidth:F0}px, max={maxWidth:F0}px, avg={avgWidth:F1}px, range={widthRange:F0}px, CV={coefficientOfVariation:F2}, ratio={minMaxRatio:F2}");
            
            // Strict uniformity checks - reject sequences with wildly different column widths
            if (coefficientOfVariation > 0.4) // Standard deviation > 40% of mean
            {
                _logger.LogInformation($"Rejecting sequence: high coefficient of variation ({coefficientOfVariation:F2} > 0.4)");
                return null;
            }
            
            if (minMaxRatio < 0.5) // Smallest column less than 50% of largest
            {
                _logger.LogInformation($"Rejecting sequence: width ratio too low ({minMaxRatio:F2} < 0.5)");
                return null;
            }
            
            if (widthRangeRatio > 1.0) // Range greater than average width
            {
                _logger.LogInformation($"Rejecting sequence: width range too large ({widthRangeRatio:F2} > 1.0)");
                return null;
            }
            
            // Calculate how much of the table width this sequence covers
            double totalWidth = region.Width;
            double sequenceWidth = sequence.Last().EndX - sequence.First().StartX;
            double coverageRatio = sequenceWidth / totalWidth;
            
            // Ideal coverage should be 80-100% of table width
            double coverageScore = 0.0;
            if (coverageRatio >= 0.8 && coverageRatio <= 1.0)
            {
                coverageScore = 1.0; // Perfect coverage
            }
            else if (coverageRatio >= 0.7 && coverageRatio < 0.8)
            {
                coverageScore = (coverageRatio - 0.7) / 0.1; // Linear scale from 70-80%
            }
            else
            {
                coverageScore = Math.Max(0, 1.0 - Math.Abs(coverageRatio - 0.9) / 0.5); // Penalty for poor coverage
            }
            
            // Calculate uniformity score based on multiple metrics
            double uniformityScore = (1.0 - coefficientOfVariation) * 0.4 + minMaxRatio * 0.4 + (1.0 - widthRangeRatio / 2.0) * 0.2;
            uniformityScore = Math.Max(0, Math.Min(1, uniformityScore));
            
            // Calculate position score (prefer columns not at the very edges)
            double sequenceStart = sequence.First().StartX - region.X;
            double sequenceEnd = sequence.Last().EndX - region.X;
            double centeredness = 1.0 - Math.Abs((sequenceStart + sequenceEnd) / 2.0 - totalWidth / 2.0) / (totalWidth / 2.0);
            
            // Combined score with emphasis on uniformity and coverage
            double score = uniformityScore * 0.5 + coverageScore * 0.4 + centeredness * 0.1;
            
            // Strict minimum thresholds - adjust based on actual number of columns
            double expectedWidth = totalWidth / sequenceCount;
            double minColumnWidth = expectedWidth * 0.4; // At least 40% of expected width
            double maxColumnWidth = expectedWidth * 2.5; // At most 250% of expected width
            
            if (coverageRatio < 0.7 || avgWidth < minColumnWidth || avgWidth > maxColumnWidth)
            {
                _logger.LogInformation($"Rejecting sequence: coverage={coverageRatio:F2}, avgWidth={avgWidth:F1}px (expected={expectedWidth:F1}px, range={minColumnWidth:F1}-{maxColumnWidth:F1}px)");
                return null;
            }
            
            // Additional check: reject if any individual column is extremely different
            foreach (var column in sequence)
            {
                double deviationFromExpected = Math.Abs(column.Width - expectedWidth) / expectedWidth;
                if (deviationFromExpected > 1.5) // More than 150% deviation
                {
                    _logger.LogInformation($"Rejecting sequence: column {column.Index} width {column.Width}px deviates {deviationFromExpected:P0} from expected {expectedWidth:F1}px");
                    return null;
                }
            }
            
            _logger.LogInformation($"Candidate sequence: coverage={coverageRatio:F2}, avgWidth={avgWidth:F1}px, uniformity={uniformityScore:F2}, score={score:F2}");
            
            return new ColumnSequence
            {
                Columns = sequence,
                StartX = sequence.First().StartX,
                EndX = sequence.Last().EndX,
                AverageWidth = avgWidth,
                WidthUniformity = uniformityScore,
                Centeredness = centeredness,
                SizeScore = coverageScore,
                Score = score
            };
        }

        /// <summary>
        /// Creates equal-division columns as fallback.
        /// </summary>
        private List<int> CreateEqualDivisionColumns(Rectangle region, int columnCount)
        {
            var boundaries = new List<int>();
            for (int i = 0; i <= columnCount; i++)
            {
                boundaries.Add(region.X + i * region.Width / columnCount);
            }
            return boundaries;
        }

        /// <summary>
        /// Smooths a profile array with specified window size.
        /// </summary>
        private double[] SmoothProfile(double[] profile, int windowSize)
        {
            var smoothed = new double[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(profile.Length - 1, i + windowSize / 2);
                double sum = 0;
                int count = 0;
                for (int j = start; j <= end; j++)
                {
                    sum += profile[j];
                    count++;
                }
                smoothed[i] = sum / count;
            }
            return smoothed;
        }

        /// <summary>
        /// Splits the input image into vertical columns based on projection profile and returns the column boundaries within a specified region.
        /// </summary>
        /// <param name="image">The chess moves image</param>
        /// <param name="expectedColumns">Expected number of columns (default: 6)</param>
        /// <param name="searchRegion">Rectangle to search within (null for entire image)</param>
        /// <returns>List of column boundary indices (pixel positions relative to image)</returns>
        public List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns, Rectangle? searchRegion)
        {
            // Determine the region to search within
            Rectangle region = searchRegion ?? new Rectangle(0, 0, image.Width, image.Height);
            
            _logger.LogInformation($"Searching for columns within region: X={region.X}, Y={region.Y}, Width={region.Width}, Height={region.Height}");
            
            image.Mutate(x => x.Grayscale());
            int regionWidth = region.Width;
            int regionHeight = region.Height;
            double[] columnSums = new double[regionWidth];
            
            // Binarize and sum binary values per column within the search region
            for (int relativeX = 0; relativeX < regionWidth; relativeX++)
            {
                int absoluteX = region.X + relativeX;
                if (absoluteX >= image.Width) break;
                
                double sum = 0;
                for (int relativeY = 0; relativeY < regionHeight; relativeY++)
                {
                    int absoluteY = region.Y + relativeY;
                    if (absoluteY >= image.Height) break;
                    
                    var pixel = image[absoluteX, absoluteY];
                    int gray = pixel.R;
                    int binary = gray > 128 ? 1 : 0; // Threshold at 128
                    sum += binary;
                }
                columnSums[relativeX] = sum;
            }
            
            int smoothWindow = Math.Max(3, regionWidth / 100);
            double[] smoothed = new double[regionWidth];
            for (int x = 0; x < regionWidth; x++)
            {
                int start = Math.Max(0, x - smoothWindow);
                int end = Math.Min(regionWidth - 1, x + smoothWindow);
                double avg = 0;
                for (int i = start; i <= end; i++) avg += columnSums[i];
                smoothed[x] = avg / (end - start + 1);
            }
            
            // Detect boundaries within the region
            List<int> detectedBoundaries = new List<int> { 0 };
            double threshold = 3.0;
            for (int x = 1; x < regionWidth - 1; x++)
            {
                double diff = Math.Abs(smoothed[x] - smoothed[x - 1]) + Math.Abs(smoothed[x] - smoothed[x + 1]);
                bool bothNeighborsAreNegative = (smoothed[x] - smoothed[x - 1] < 0 ) && (smoothed[x] - smoothed[x + 1] < 0);
                if (diff > threshold && bothNeighborsAreNegative)
                {
                    detectedBoundaries.Add(x);
                }
            }
            detectedBoundaries.Add(regionWidth);
            detectedBoundaries = detectedBoundaries.Distinct().OrderBy(b => b).ToList();

            // Step 1: Create dummy slices (regionWidth/expectedColumns)
            List<int> dummyEdges = new List<int>();
            for (int i = 0; i <= expectedColumns; i++)
            {
                dummyEdges.Add(i * regionWidth / expectedColumns);
            }

            // Step 2: For each dummy edge (except 0 and regionWidth), adjust to closest detected boundary if within range
            int maxAdjust = regionWidth / (expectedColumns * 2);
            List<int> finalEdges = new List<int> { 0 };
            for (int i = 1; i < dummyEdges.Count - 1; i++)
            {
                int dummyEdge = dummyEdges[i];
                int closestBoundary = detectedBoundaries
                    .OrderBy(b => Math.Abs(b - dummyEdge))
                    .FirstOrDefault();
                if (Math.Abs(closestBoundary - dummyEdge) <= maxAdjust)
                {
                    // Prefer larger slices: if boundary is to the right, use it, else use dummyEdge
                    if (closestBoundary > dummyEdge)
                        finalEdges.Add(closestBoundary);
                    else
                        finalEdges.Add(dummyEdge);
                }
                else
                {
                    finalEdges.Add(dummyEdge);
                }
            }
            finalEdges.Add(regionWidth);

            // Ensure edges are sorted and unique, and at least 1 pixel apart
            finalEdges = finalEdges.Distinct().OrderBy(e => e).ToList();
            for (int i = finalEdges.Count - 1; i > 0; i--)
            {
                if (finalEdges[i] - finalEdges[i - 1] < 1)
                {
                    finalEdges.RemoveAt(i);
                }
            }

            // Convert relative positions back to absolute positions
            for (int i = 0; i < finalEdges.Count; i++)
            {
                finalEdges[i] += region.X;
            }

            _logger.LogInformation($"Found {finalEdges.Count - 1} column boundaries within the search region");

            return finalEdges;
        }

        /// <summary>
        /// Splits the input image into vertical columns based on projection profile and returns the column boundaries.
        /// </summary>
        /// <param name="imagePath">Path to the chess moves image</param>
        /// <param name="expectedColumns">Expected number of columns (default: 6)</param>
        /// <returns>List of column boundary indices (pixel positions)</returns>
        public List<int> SplitImageIntoColumns(string imagePath, int expectedColumns = 6)
        {
            using var image = Image.Load<Rgba32>(imagePath);
            return SplitImageIntoColumns(image, expectedColumns);
        }

        


        /// <summary>
        /// Crops an image to the specified rectangle boundaries.
        /// </summary>
        /// <param name="imagePath">Path to the image to crop</param>
        /// <param name="x">X coordinate of the crop area</param>
        /// <param name="y">Y coordinate of the crop area</param>
        /// <param name="width">Width of the crop area</param>
        /// <param name="height">Height of the crop area</param>
        /// <returns>Byte array of the cropped image</returns>
        public async Task<byte[]> CropImageAsync(string imagePath, int x, int y, int width, int height)
        {
            using var image = Image.Load<Rgba32>(imagePath);
            
            // Validate crop boundaries
            x = Math.Max(0, Math.Min(x, image.Width - 1));
            y = Math.Max(0, Math.Min(y, image.Height - 1));
            width = Math.Max(1, Math.Min(width, image.Width - x));
            height = Math.Max(1, Math.Min(height, image.Height - y));
            
            _logger.LogInformation($"Cropping image from ({x}, {y}) with size {width}x{height}");
            
            // Create crop rectangle
            var cropRectangle = new Rectangle(x, y, width, height);
            
            // Crop the image
            using var croppedImage = image.Clone(ctx => ctx.Crop(cropRectangle));
            
            // Convert to byte array
            using var ms = new MemoryStream();
            await croppedImage.SaveAsJpegAsync(ms);
            return ms.ToArray();
        }

        /// <summary>
        /// Finds the external boundaries of the chess notation table in the image using content analysis.
        /// </summary>
        /// <param name="image">The image to analyze</param>
        /// <returns>Rectangle representing the table boundaries</returns>
        public Rectangle FindTableBoundaries(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            
            _logger.LogInformation($"Analyzing table boundaries for image {width}x{height}");
            
            // Try multiple approaches to find the best table boundaries
         /*   var approaches = new List<(string name, Rectangle bounds)>
            {
                ("Content Density Analysis", FindTableByContentDensity(image)),
                ("Text Block Detection", FindTableByTextBlocks(image)),
                ("Morphological Analysis", FindTableByMorphology(image)),
                ("Projection Profile Fallback", FindTableBoundariesFallback(image))
            };
            
            // Score each approach and select the best one
            var bestApproach = approaches
                .Where(a => a.bounds.Width > 0 && a.bounds.Height > 0)
                .OrderByDescending(a => ScoreTableBoundary(a.bounds, width, height))
                .FirstOrDefault();
            
            if (bestApproach.bounds.Width > 0 && bestApproach.bounds.Height > 0)
            {
                _logger.LogInformation($"Selected {bestApproach.name} - X: {bestApproach.bounds.X}, Y: {bestApproach.bounds.Y}, Width: {bestApproach.bounds.Width}, Height: {bestApproach.bounds.Height}");
                return bestApproach.bounds;
            }
            
            _logger.LogWarning("All approaches failed, using fallback"); */
            return FindTableByMorphology(image);
        }
        
        /// <summary>
        /// Finds table boundaries by analyzing content density patterns.
        /// </summary>
        private Rectangle FindTableByContentDensity(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            
            // Convert to grayscale and enhance contrast
            using var processedImage = image.Clone();
            processedImage.Mutate(x => x.Grayscale().Contrast(1.5f));
            
            // Calculate content density using a sliding window approach
            int windowSize = Math.Min(width, height) / 200; // Adaptive window size
            var densityMap = CalculateContentDensity(processedImage, windowSize);
            
            // Find the region with highest content density
            var maxDensityRegion = FindMaxDensityRegion(densityMap, windowSize);
            
            _logger.LogInformation($"Content density analysis found region: {maxDensityRegion}");
            return maxDensityRegion;
        }
        
        /// <summary>
        /// Finds table boundaries by detecting text blocks and their arrangement.
        /// </summary>
        private Rectangle FindTableByTextBlocks(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            
            // Convert to grayscale and binarize
            using var processedImage = image.Clone();
            processedImage.Mutate(x => x.Grayscale().BinaryThreshold(0.5f));
            
            // Find connected components (text blocks)
            var textBlocks = FindConnectedComponents(processedImage);
            
            if (textBlocks.Count == 0)
            {
                _logger.LogWarning("No text blocks found in text block detection");
                return Rectangle.Empty;
            }
            
            
            // Calculate bounding rectangle of all text blocks
            var bounds = CalculateBoundingRectangle(textBlocks);
            
            _logger.LogInformation($"Text block detection found {textBlocks.Count} blocks, bounds: {bounds}");
            return bounds;
        }
        
        /// <summary>
        /// Finds table boundaries using morphological operations.
        /// </summary>
        private Rectangle FindTableByMorphology(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            
            // Convert to grayscale and binarize
            using var processedImage = image.Clone();
            processedImage.Mutate(x => x.Grayscale().BinaryThreshold(0.5f));
            
            // Apply morphological operations to find table structure
            using var morphedImage = processedImage.Clone();
            ApplyMorphologicalOperations(morphedImage);
            
            // Find the largest connected component (likely the table)
            var largestComponent = FindLargestConnectedComponent(morphedImage);
            
            _logger.LogInformation($"Morphological analysis found component: {largestComponent}");
            return largestComponent;
        }
        
        /// <summary>
        /// Scores a table boundary based on various criteria.
        /// </summary>
        private double ScoreTableBoundary(Rectangle bounds, int imageWidth, int imageHeight)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return 0;
            
            // Prefer boundaries that are not too close to edges (avoid full image)
            double edgeDistance = Math.Min(
                Math.Min(bounds.X, imageWidth - bounds.Right),
                Math.Min(bounds.Y, imageHeight - bounds.Bottom)
            );
            
            // Prefer reasonable aspect ratios (not too wide or too tall)
            double aspectRatio = (double)bounds.Width / bounds.Height;
            double aspectScore = 1.0 - Math.Abs(aspectRatio - 2.0) / 2.0; // Prefer aspect ratio around 2:1
            
            // Prefer reasonable size (not too small, not too large)
            double sizeScore = Math.Min(1.0, (double)(bounds.Width * bounds.Height) / (imageWidth * imageHeight * 0.1));
            
            // Combine scores
            return edgeDistance * 0.3 + aspectScore * 0.4 + sizeScore * 0.3;
        }
        
        /// <summary>
        /// Calculates content density using a sliding window approach.
        /// </summary>
        private double[,] CalculateContentDensity(Image<Rgba32> image, int windowSize)
        {
            int width = image.Width;
            int height = image.Height;
            int cols = width / windowSize;
            int rows = height / windowSize;
            
            var densityMap = new double[rows, cols];
            
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int startX = col * windowSize;
                    int startY = row * windowSize;
                    int endX = Math.Min(startX + windowSize, width);
                    int endY = Math.Min(startY + windowSize, height);
                    
                    int darkPixels = 0;
                    int totalPixels = 0;
                    
                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            var pixel = image[x, y];
                            if (pixel.R < 128) // Dark pixel threshold
                            {
                                darkPixels++;
                            }
                            totalPixels++;
                        }
                    }
                    
                    densityMap[row, col] = totalPixels > 0 ? (double)darkPixels / totalPixels : 0;
                }
            }
            
            return densityMap;
        }
        
        /// <summary>
        /// Finds the region with maximum content density.
        /// </summary>
        private Rectangle FindMaxDensityRegion(double[,] densityMap, int windowSize)
        {
            int rows = densityMap.GetLength(0);
            int cols = densityMap.GetLength(1);
            
            double maxDensity = 0;
            int maxRow = 0, maxCol = 0;
            
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (densityMap[row, col] > maxDensity)
                    {
                        maxDensity = densityMap[row, col];
                        maxRow = row;
                        maxCol = col;
                    }
                }
            }
            
            // Expand the region to include nearby high-density areas
            int minRow = maxRow, maxRowEnd = maxRow;
            int minCol = maxCol, maxColEnd = maxCol;
            
            double threshold = maxDensity * 0.7; // 70% of max density
            
            // Expand vertically
            for (int row = maxRow - 1; row >= 0; row--)
            {
                bool foundHighDensity = false;
                for (int col = 0; col < cols; col++)
                {
                    if (densityMap[row, col] >= threshold)
                    {
                        foundHighDensity = true;
                        break;
                    }
                }
                if (foundHighDensity) minRow = row; else break;
            }
            
            for (int row = maxRow + 1; row < rows; row++)
            {
                bool foundHighDensity = false;
                for (int col = 0; col < cols; col++)
                {
                    if (densityMap[row, col] >= threshold)
                    {
                        foundHighDensity = true;
                        break;
                    }
                }
                if (foundHighDensity) maxRowEnd = row; else break;
            }
            
            // Expand horizontally
            for (int col = maxCol - 1; col >= 0; col--)
            {
                bool foundHighDensity = false;
                for (int row = 0; row < rows; row++)
                {
                    if (densityMap[row, col] >= threshold)
                    {
                        foundHighDensity = true;
                        break;
                    }
                }
                if (foundHighDensity) minCol = col; else break;
            }
            
            for (int col = maxCol + 1; col < cols; col++)
            {
                bool foundHighDensity = false;
                for (int row = 0; row < rows; row++)
                {
                    if (densityMap[row, col] >= threshold)
                    {
                        foundHighDensity = true;
                        break;
                    }
                }
                if (foundHighDensity) maxColEnd = col; else break;
            }
            
            return new Rectangle(
                minCol * windowSize,
                minRow * windowSize,
                (maxColEnd - minCol + 1) * windowSize,
                (maxRowEnd - minRow + 1) * windowSize
            );
        }
        
        /// <summary>
        /// Fallback method using projection profiles when corner detection fails.
        /// </summary>
        private Rectangle FindTableBoundariesFallback(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            
            // Convert to grayscale for analysis
            using var grayscaleImage = image.Clone();
            grayscaleImage.Mutate(x => x.Grayscale());
            
            
            // Calculate horizontal projection profile (sum of pixel intensities per row)
            double[] horizontalProfile = new double[height];
            for (int y = 0; y < height; y++)
            {
                double sum = 0;
                for (int x = 0; x < width; x++)
                {
                    var pixel = grayscaleImage[x, y];
                    int gray = pixel.R;
                    int binary = gray < 200 ? 1 : 0; // Invert threshold - look for dark content
                    sum += binary;
                }
                horizontalProfile[y] = sum;
            }
            
            // Calculate vertical projection profile (sum of pixel intensities per column)
            double[] verticalProfile = new double[width];
            for (int x = 0; x < width; x++)
            {
                double sum = 0;
                for (int y = 0; y < height; y++)
                {
                    var pixel = grayscaleImage[x, y];
                    int gray = pixel.R;
                    int binary = gray < 200 ? 1 : 0; // Invert threshold - look for dark content
                    sum += binary;
                }
                verticalProfile[x] = sum;
            }
            
            // Find table boundaries using improved algorithm
            int top = FindTableEdgeImproved(horizontalProfile, true, height);
            int bottom = FindTableEdgeImproved(horizontalProfile, false, height);
            int left = FindTableEdgeImproved(verticalProfile, true, width);
            int right = FindTableEdgeImproved(verticalProfile, false, width);
            
            // Ensure boundaries are within image bounds
            top = Math.Max(0, top);
            bottom = Math.Min(height - 1, bottom);
            left = Math.Max(0, left);
            right = Math.Min(width - 1, right);
            
            var result = new Rectangle(left, top, right - left, bottom - top);
            _logger.LogInformation($"Fallback table boundaries - X: {result.X}, Y: {result.Y}, Width: {result.Width}, Height: {result.Height}");
            
            return result;
        }
        
        /// <summary>
        /// Finds the four corners of the chess notation grid using edge detection and line intersection.
        /// </summary>
        /// <param name="edgeImage">Edge-detected image</param>
        /// <param name="width">Image width</param>
        /// <param name="height">Image height</param>
        /// <returns>List of detected corner points</returns>
        private List<Point> FindGridCorners(Image<Rgba32> edgeImage, int width, int height)
        {
            var corners = new List<Point>();
            
            // Find vertical lines
            var verticalLines = FindVerticalLines(edgeImage, width, height);
            
            _logger.LogInformation($"Found {verticalLines.Count} vertical lines");
            
            // For now, just return empty corners since we removed horizontal line detection
            // This is a simplified version that only works with vertical lines
            var intersections = new List<Point>();
            
            // Filter intersections to find the most likely grid corners
            corners = FilterGridCorners(intersections, width, height);
            
            _logger.LogInformation($"Filtered to {corners.Count} potential grid corners");
            
            return corners;
        }
        
        
        /// <summary>
        /// Finds vertical lines in the edge-detected image.
        /// </summary>
        private List<Line> FindVerticalLines(Image<Rgba32> edgeImage, int width, int height)
        {
            var lines = new List<Line>();
            int minLineLength = height / 4; // Minimum line length as fraction of image height
            int lineThickness = 3; // Allow for some thickness in line detection
            
            // Scan vertically for lines
            for (int x = 0; x < width; x++)
            {
                int lineStart = -1;
                int consecutivePixels = 0;
                
                for (int y = 0; y < height; y++)
                {
                    var pixel = edgeImage[x, y];
                    bool isEdge = pixel.R > 128; // Edge threshold
                    
                    if (isEdge)
                    {
                        if (lineStart == -1)
                        {
                            lineStart = y;
                        }
                        consecutivePixels++;
                    }
                    else
                    {
                        if (consecutivePixels >= minLineLength)
                        {
                            // Found a vertical line
                            lines.Add(new Line { Start = new Point(x, lineStart), End = new Point(x, y - 1) });
                        }
                        lineStart = -1;
                        consecutivePixels = 0;
                    }
                }
                
                // Check if line extends to end of image
                if (consecutivePixels >= minLineLength)
                {
                    lines.Add(new Line { Start = new Point(x, lineStart), End = new Point(x, height - 1) });
                }
            }
            
            // Merge nearby vertical lines (within lineThickness pixels)
            return MergeNearbyLines(lines, lineThickness);
        }
        
        /// <summary>
        /// Merges nearby vertical lines to reduce noise and duplicate detections.
        /// </summary>
        private List<Line> MergeNearbyLines(List<Line> lines, int threshold)
        {
            if (lines.Count == 0) return lines;
            
            var mergedLines = new List<Line>();
            var used = new bool[lines.Count];
            
            for (int i = 0; i < lines.Count; i++)
            {
                if (used[i]) continue;
                
                var currentLine = lines[i];
                var mergedLine = currentLine;
                used[i] = true;
                
                // Find nearby lines to merge
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (used[j]) continue;
                    
                    var otherLine = lines[j];
                    
                    // Check if vertical lines are close enough to merge (X coordinates)
                    bool shouldMerge = Math.Abs(currentLine.Start.X - otherLine.Start.X) <= threshold;
                    
                    if (shouldMerge)
                    {
                        // Merge the lines by extending the current line vertically
                        mergedLine.Start = new Point(mergedLine.Start.X, Math.Min(mergedLine.Start.Y, otherLine.Start.Y));
                        mergedLine.End = new Point(mergedLine.End.X, Math.Max(mergedLine.End.Y, otherLine.End.Y));
                        used[j] = true;
                    }
                }
                
                mergedLines.Add(mergedLine);
            }
            
            return mergedLines;
        }
        
        
        
        /// <summary>
        /// Filters intersections to find the most likely grid corners.
        /// </summary>
        private List<Point> FilterGridCorners(List<Point> intersections, int width, int height)
        {
            if (intersections.Count <= 4)
                return intersections;
            
            // Group nearby intersections to avoid duplicates
            var groupedCorners = new List<Point>();
            var used = new bool[intersections.Count];
            int groupingRadius = Math.Min(width, height) / 20; // Adaptive grouping radius
            
            for (int i = 0; i < intersections.Count; i++)
            {
                if (used[i]) continue;
                
                var currentPoint = intersections[i];
                var group = new List<Point> { currentPoint };
                used[i] = true;
                
                // Find nearby points to group
                for (int j = i + 1; j < intersections.Count; j++)
                {
                    if (used[j]) continue;
                    
                    var otherPoint = intersections[j];
                    double distance = Math.Sqrt(Math.Pow(currentPoint.X - otherPoint.X, 2) + Math.Pow(currentPoint.Y - otherPoint.Y, 2));
                    
                    if (distance <= groupingRadius)
                    {
                        group.Add(otherPoint);
                        used[j] = true;
                    }
                }
                
                // Use the centroid of the group as the corner
                int avgX = (int)group.Average(p => p.X);
                int avgY = (int)group.Average(p => p.Y);
                groupedCorners.Add(new Point(avgX, avgY));
            }
            
            // If we have more than 4 corners, select the 4 most likely ones
            if (groupedCorners.Count > 4)
            {
                // Score corners based on their position (prefer corners near image edges)
                var scoredCorners = groupedCorners.Select(corner => new
                {
                    Point = corner,
                    Score = CalculateCornerScore(corner, width, height)
                }).OrderByDescending(x => x.Score).Take(4).Select(x => x.Point).ToList();
                
                return scoredCorners;
            }
            
            return groupedCorners;
        }
        
        /// <summary>
        /// Calculates a score for a corner point based on its position.
        /// Higher scores indicate more likely grid corners.
        /// </summary>
        private double CalculateCornerScore(Point corner, int width, int height)
        {
            // Prefer corners that are near the edges of the image
            double distanceFromEdges = Math.Min(
                Math.Min(corner.X, width - corner.X),
                Math.Min(corner.Y, height - corner.Y)
            );
            
            // Prefer corners that are not too close to the center
            double distanceFromCenter = Math.Sqrt(
                Math.Pow(corner.X - width / 2, 2) + Math.Pow(corner.Y - height / 2, 2)
            );
            
            // Score based on distance from edges (closer is better) and distance from center (farther is better)
            return distanceFromCenter / (distanceFromEdges + 1);
        }
        
        /// <summary>
        /// Sorts corners to identify top-left, top-right, bottom-left, bottom-right.
        /// </summary>
        private List<Point> SortCorners(List<Point> corners)
        {
            if (corners.Count != 4)
                return corners;
            
            // Sort by Y coordinate first (top vs bottom)
            var sortedByY = corners.OrderBy(p => p.Y).ToList();
            
            // Split into top and bottom pairs
            var topCorners = sortedByY.Take(2).OrderBy(p => p.X).ToList();
            var bottomCorners = sortedByY.Skip(2).OrderBy(p => p.X).ToList();
            
            // Return in order: top-left, top-right, bottom-left, bottom-right
            return new List<Point>
            {
                topCorners[0],    // top-left
                topCorners[1],    // top-right
                bottomCorners[0], // bottom-left
                bottomCorners[1]  // bottom-right
            };
        }
        
        /// <summary>
        /// Finds connected components (text blocks) in a binary image.
        /// </summary>
        private List<Rectangle> FindConnectedComponents(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            var visited = new bool[height, width];
            var components = new List<Rectangle>();
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!visited[y, x] && IsDarkPixel(image[x, y]))
                    {
                        var component = FloodFill(image, x, y, visited);
                        if (component.Width > 5 && component.Height > 5) // Filter small noise
                        {
                            components.Add(component);
                        }
                    }
                }
            }
            
            return components;
        }
        
        /// <summary>
        /// Performs flood fill to find a connected component.
        /// </summary>
        private Rectangle FloodFill(Image<Rgba32> image, int startX, int startY, bool[,] visited)
        {
            int width = image.Width;
            int height = image.Height;
            var stack = new Stack<(int x, int y)>();
            stack.Push((startX, startY));
            
            int minX = startX, maxX = startX;
            int minY = startY, maxY = startY;
            
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                
                if (x < 0 || x >= width || y < 0 || y >= height || visited[y, x] || !IsDarkPixel(image[x, y]))
                    continue;
                
                visited[y, x] = true;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
                
                // Add 8-connected neighbors
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        stack.Push((x + dx, y + dy));
                    }
                }
            }
            
            return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        
        /// <summary>
        /// Checks if a pixel is considered dark (text).
        /// </summary>
        private bool IsDarkPixel(Rgba32 pixel)
        {
            return pixel.R < 128; // Simple threshold
        }
        
        
        /// <summary>
        /// Calculates bounding rectangle of a list of rectangles.
        /// </summary>
        private Rectangle CalculateBoundingRectangle(List<Rectangle> rectangles)
        {
            if (rectangles.Count == 0)
                return Rectangle.Empty;
            
            int minX = rectangles.Min(r => r.X);
            int maxX = rectangles.Max(r => r.Right);
            int minY = rectangles.Min(r => r.Y);
            int maxY = rectangles.Max(r => r.Bottom);
            
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
        
        /// <summary>
        /// Applies morphological operations to enhance table structure.
        /// </summary>
        private void ApplyMorphologicalOperations(Image<Rgba32> image)
        {
            // Simple dilation to connect nearby text elements
            int width = image.Width;
            int height = image.Height;
            var temp = new bool[height, width];
            
            // Copy current state
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    temp[y, x] = IsDarkPixel(image[x, y]);
                }
            }
            
            // Apply dilation
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    if (temp[y, x])
                    {
                        // Dilate to 3x3 neighborhood
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    image[nx, ny] = new Rgba32(0, 0, 0, 255); // Black
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Finds the largest connected component in the image.
        /// </summary>
        private Rectangle FindLargestConnectedComponent(Image<Rgba32> image)
        {
            var components = FindConnectedComponents(image);
            
            if (components.Count == 0)
                return Rectangle.Empty;
            
            return components.OrderByDescending(c => c.Width * c.Height).First();
        }
        
        /// <summary>
        /// Applies Sobel edge detection to the image.
        /// </summary>
        /// <param name="image">The grayscale image to apply edge detection to</param>
        private void ApplySobelEdgeDetection(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            
            // Sobel kernels
            int[,] sobelX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] sobelY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
            
            // Create a copy for the result
            using var resultImage = image.Clone();
            
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int gx = 0, gy = 0;
                    
                    // Apply Sobel kernels
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            var pixel = image[x + kx, y + ky];
                            int gray = pixel.R; // Assuming grayscale, so R=G=B
                            
                            gx += gray * sobelX[ky + 1, kx + 1];
                            gy += gray * sobelY[ky + 1, kx + 1];
                        }
                    }
                    
                    // Calculate gradient magnitude
                    int magnitude = (int)Math.Sqrt(gx * gx + gy * gy);
                    magnitude = Math.Min(255, magnitude); // Clamp to 255
                    
                    // Set the result
                    resultImage[x, y] = new Rgba32((byte)magnitude, (byte)magnitude, (byte)magnitude, 255);
                }
            }
            
            // Copy the result back to the original image
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    image[x, y] = resultImage[x, y];
                }
            }
        }
        
        /// <summary>
        /// Represents a line segment for intersection calculations.
        /// </summary>
        private class Line
        {
            public Point Start { get; set; }
            public Point End { get; set; }
        }
        
        /// <summary>
        /// Improved helper method to find table edges using projection profiles.
        /// </summary>
        /// <param name="profile">Projection profile array</param>
        /// <param name="fromStart">True to search from start, false to search from end</param>
        /// <param name="dimension">Width or height for context</param>
        /// <returns>Edge position</returns>
        private int FindTableEdgeImproved(double[] profile, bool fromStart, int dimension)
        {
            int length = profile.Length;
            
            // Calculate a more sophisticated threshold
            double maxValue = profile.Max();
            double avgValue = profile.Average();
            double threshold = Math.Max(maxValue * 0.3, avgValue * 1.5); // Higher threshold to avoid noise
            
            _logger.LogInformation($"Edge detection - Direction: {(fromStart ? "Start" : "End")}, Threshold: {threshold:F2}, Max: {maxValue:F2}, Avg: {avgValue:F2}");
            
            // Smooth the profile to reduce noise
            double[] smoothedProfile = SmoothProfile(profile);
            
            if (fromStart)
            {
                // Find first significant peak from the start, but skip only the very first pixels
                int startSearch = Math.Max(0, 10); // Skip only first 10 pixels
                int endSearch = length / 2; // Only search first half
                
                _logger.LogInformation($"Searching from {startSearch} to {endSearch} (first half)");
                
                for (int i = startSearch; i < endSearch; i++)
                {
                    if (smoothedProfile[i] > threshold)
                    {
                        _logger.LogInformation($"Found candidate at position {i} with value {smoothedProfile[i]:F2}");
                        
                        // Look for the actual start of the table (where content becomes consistent)
                        for (int j = i; j >= startSearch; j--)
                        {
                            if (smoothedProfile[j] < threshold * 0.5)
                            {
                                _logger.LogInformation($"Found table start at position {j + 1}");
                                return j + 1;
                            }
                        }
                        _logger.LogInformation($"Using candidate position {i} as table start");
                        return i;
                    }
                }
                _logger.LogWarning($"No table edge found in first half, using fallback {length / 10}");
            }
            else
            {
                // Find last significant peak from the end, but skip only the very last pixels
                int startSearch = length / 2; // Start from middle
                int endSearch = Math.Min(length - 1, length - 10); // Skip only last 10 pixels
                
                _logger.LogInformation($"Searching from {endSearch} to {startSearch} (second half)");
                
                for (int i = endSearch; i >= startSearch; i--)
                {
                    if (smoothedProfile[i] > threshold)
                    {
                        _logger.LogInformation($"Found candidate at position {i} with value {smoothedProfile[i]:F2}");
                        
                        // Look for the actual end of the table (where content becomes consistent)
                        for (int j = i; j <= endSearch; j++)
                        {
                            if (smoothedProfile[j] < threshold * 0.5)
                            {
                                _logger.LogInformation($"Found table end at position {j - 1}");
                                return j - 1;
                            }
                        }
                        _logger.LogInformation($"Using candidate position {i} as table end");
                        return i;
                    }
                }
                _logger.LogWarning($"No table edge found in second half, using fallback {length - length / 10}");
            }
            
            return fromStart ? length / 10 : length - length / 10; // Fallback to reasonable defaults
        }
        
        /// <summary>
        /// Smooths a profile array to reduce noise.
        /// </summary>
        /// <param name="profile">Input profile array</param>
        /// <returns>Smoothed profile array</returns>
        private double[] SmoothProfile(double[] profile)
        {
            int length = profile.Length;
            double[] smoothed = new double[length];
            int windowSize = Math.Max(3, length / 100); // Adaptive window size
            
            for (int i = 0; i < length; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(length - 1, i + windowSize / 2);
                double sum = 0;
                int count = 0;
                
                for (int j = start; j <= end; j++)
                {
                    sum += profile[j];
                    count++;
                }
                
                smoothed[i] = sum / count;
            }
            
            return smoothed;
        }

        /// <summary>
        /// Draws table boundaries on the image for visualization.
        /// </summary>
        /// <param name="image">The image to draw boundaries on</param>
        /// <param name="tableBoundaries">Rectangle representing the table boundaries</param>
        private void DrawTableBoundariesOnImage(Image<Rgba32> image, Rectangle tableBoundaries)
        {
            var blue = new Rgba32(0, 0, 255, 255); // Blue color
            int thickness = 6; // Thicker than column boundaries
            
            // Draw top boundary
            for (int x = tableBoundaries.X; x < tableBoundaries.X + tableBoundaries.Width; x++)
            {
                for (int offset = 0; offset < thickness; offset++)
                {
                    int y = tableBoundaries.Y + offset;
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        image[x, y] = blue;
                    }
                }
            }
            
            // Draw bottom boundary
            for (int x = tableBoundaries.X; x < tableBoundaries.X + tableBoundaries.Width; x++)
            {
                for (int offset = 0; offset < thickness; offset++)
                {
                    int y = tableBoundaries.Y + tableBoundaries.Height - offset;
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        image[x, y] = blue;
                    }
                }
            }
            
            // Draw left boundary
            for (int y = tableBoundaries.Y; y < tableBoundaries.Y + tableBoundaries.Height; y++)
            {
                for (int offset = 0; offset < thickness; offset++)
                {
                    int x = tableBoundaries.X + offset;
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        image[x, y] = blue;
                    }
                }
            }
            
            // Draw right boundary
            for (int y = tableBoundaries.Y; y < tableBoundaries.Y + tableBoundaries.Height; y++)
            {
                for (int offset = 0; offset < thickness; offset++)
                {
                    int x = tableBoundaries.X + tableBoundaries.Width - offset;
                    if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                    {
                        image[x, y] = blue;
                    }
                }
            }
        }

        /// <summary>
        /// Draws column boundaries on the image for visualization.
        /// </summary>
        /// <param name="image">The image to draw boundaries on</param>
        /// <param name="boundaries">List of column boundary positions</param>
        private void DrawBoundariesOnImage(Image<Rgba32> image, List<int> boundaries)
        {
            int height = image.Height;
            var red = new Rgba32(255, 0, 0, 255); // Red color
            
            // Draw vertical lines for each boundary (except the first and last which are edges)
            for (int i = 1; i < boundaries.Count - 1; i++)
            {
                int x = boundaries[i];
                
                // Draw thick vertical line (5-pixel wide for better visibility)
                for (int y = 0; y < height; y++)
                {
                    // Draw 5-pixel wide line
                    for (int offset = -2; offset <= 2; offset++)
                    {
                        int lineX = x + offset;
                        if (lineX >= 0 && lineX < image.Width)
                        {
                            image[lineX, y] = red;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws ALL column boundaries including column 0 and the last column for better visualization.
        /// </summary>
        /// <param name="image">The image to draw boundaries on</param>
        /// <param name="boundaries">List of column boundary positions</param>
        /// <param name="tableBoundaries">Table boundaries to constrain drawing</param>
        private void DrawAllColumnBoundaries(Image<Rgba32> image, List<int> boundaries, Rectangle tableBoundaries)
        {
            var red = new Rgba32(255, 0, 0, 255); // Red color
            int thickness = 3; // 3-pixel wide for better visibility
            
            // Draw vertical lines for ALL boundaries including first (column 0) and last
            for (int i = 0; i < boundaries.Count; i++)
            {
                int x = boundaries[i];
                
                // Only draw within the table boundaries vertically
                int startY = tableBoundaries.Y;
                int endY = tableBoundaries.Y + tableBoundaries.Height;
                
                // Draw thick vertical line
                for (int y = startY; y < endY; y++)
                {
                    for (int offset = -(thickness/2); offset <= thickness/2; offset++)
                    {
                        int lineX = x + offset;
                        if (lineX >= 0 && lineX < image.Width && y >= 0 && y < image.Height)
                        {
                            image[lineX, y] = red;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Grays out areas outside the detected table boundaries to show which parts are excluded from processing.
        /// </summary>
        /// <param name="image">The image to modify</param>
        /// <param name="tableBoundaries">The detected table boundaries</param>
        private void GrayOutAreasOutsideTable(Image<Rgba32> image, Rectangle tableBoundaries)
        {
            // Call the overloaded method with empty column boundaries for backwards compatibility
            GrayOutAreasOutsideTable(image, tableBoundaries, new List<int>());
        }

        /// <summary>
        /// Grays out areas outside the detected table boundaries and outside the column processing area to show which parts are excluded from processing.
        /// </summary>
        /// <param name="image">The image to modify</param>
        /// <param name="tableBoundaries">The detected table boundaries</param>
        /// <param name="columnBoundaries">The detected column boundaries</param>
        private void GrayOutAreasOutsideTable(Image<Rgba32> image, Rectangle tableBoundaries, List<int> columnBoundaries)
        {
            var grayOverlay = new Rgba32(128, 128, 128, 128); // Semi-transparent gray
            
            // Gray out area to the left of the table
            for (int x = 0; x < tableBoundaries.X; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    var originalPixel = image[x, y];
                    // Blend with gray overlay to create a grayed-out effect
                    var blendedPixel = BlendPixels(originalPixel, grayOverlay);
                    image[x, y] = blendedPixel;
                }
            }
            
            // Gray out area between table left boundary and column 0 (if there's a gap)
            if (columnBoundaries.Count > 0)
            {
                int column0X = columnBoundaries[0];
                if (column0X > tableBoundaries.X)
                {
                    for (int x = tableBoundaries.X; x < column0X; x++)
                    {
                        for (int y = tableBoundaries.Y; y < tableBoundaries.Y + tableBoundaries.Height; y++)
                        {
                            var originalPixel = image[x, y];
                            var blendedPixel = BlendPixels(originalPixel, grayOverlay);
                            image[x, y] = blendedPixel;
                        }
                    }
                }
            }
            
            // Gray out area between last column and table right boundary (if there's a gap)
            if (columnBoundaries.Count > 0)
            {
                int lastColumnX = columnBoundaries[columnBoundaries.Count - 1];
                int tableRight = tableBoundaries.X + tableBoundaries.Width;
                if (lastColumnX < tableRight)
                {
                    for (int x = lastColumnX; x < tableRight; x++)
                    {
                        for (int y = tableBoundaries.Y; y < tableBoundaries.Y + tableBoundaries.Height; y++)
                        {
                            var originalPixel = image[x, y];
                            var blendedPixel = BlendPixels(originalPixel, grayOverlay);
                            image[x, y] = blendedPixel;
                        }
                    }
                }
            }
            
            // Gray out area to the right of the table
            int tableRightBoundary = tableBoundaries.X + tableBoundaries.Width;
            for (int x = tableRightBoundary; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    var originalPixel = image[x, y];
                    var blendedPixel = BlendPixels(originalPixel, grayOverlay);
                    image[x, y] = blendedPixel;
                }
            }
            
            // Gray out area above the table
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < tableBoundaries.Y; y++)
                {
                    var originalPixel = image[x, y];
                    var blendedPixel = BlendPixels(originalPixel, grayOverlay);
                    image[x, y] = blendedPixel;
                }
            }
            
            // Gray out area below the table
            int tableBottom = tableBoundaries.Y + tableBoundaries.Height;
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = tableBottom; y < image.Height; y++)
                {
                    var originalPixel = image[x, y];
                    var blendedPixel = BlendPixels(originalPixel, grayOverlay);
                    image[x, y] = blendedPixel;
                }
            }
        }

        /// <summary>
        /// Blends two pixels together using alpha blending.
        /// </summary>
        /// <param name="background">Background pixel</param>
        /// <param name="overlay">Overlay pixel with alpha</param>
        /// <returns>Blended pixel</returns>
        private Rgba32 BlendPixels(Rgba32 background, Rgba32 overlay)
        {
            float alpha = overlay.A / 255.0f;
            float invAlpha = 1.0f - alpha;
            
            byte r = (byte)(background.R * invAlpha + overlay.R * alpha);
            byte g = (byte)(background.G * invAlpha + overlay.G * alpha);
            byte b = (byte)(background.B * invAlpha + overlay.B * alpha);
            
            return new Rgba32(r, g, b, 255);
        }

        
        /// <summary>
        /// Gets the detected corners of the chess notation grid for debugging purposes.
        /// </summary>
        /// <param name="image">The image to analyze</param>
        /// <returns>List of detected corner points</returns>
        public List<Point> GetDetectedCorners(Image<Rgba32> image)
        {
            // Convert to grayscale for analysis
            using var grayscaleImage = image.Clone();
            grayscaleImage.Mutate(x => x.Grayscale());
            
            // Apply edge detection to find grid lines
            using var edgeImage = grayscaleImage.Clone();
            edgeImage.Mutate(x => x.GaussianBlur(1.0f));
            ApplySobelEdgeDetection(edgeImage);
            
            // Find the four corners of the chess notation grid
            var corners = FindGridCorners(edgeImage, image.Width, image.Height);
            
            if (corners.Count >= 4)
            {
                // Sort corners to identify top-left, top-right, bottom-left, bottom-right
                return SortCorners(corners);
            }
            
            return corners;
        }
        
        /// <summary>
        /// Gets detailed corner information including corner types and confidence scores.
        /// </summary>
        /// <param name="image">The image to analyze</param>
        /// <returns>Dictionary with corner information</returns>
        public Dictionary<string, object> GetDetailedCornerInfo(Image<Rgba32> image)
        {
            var corners = GetDetectedCorners(image);
            var result = new Dictionary<string, object>();
            
            if (corners.Count >= 4)
            {
                result["success"] = true;
                result["cornerCount"] = corners.Count;
                result["corners"] = new Dictionary<string, object>
                {
                    ["topLeft"] = new { x = corners[0].X, y = corners[0].Y },
                    ["topRight"] = new { x = corners[1].X, y = corners[1].Y },
                    ["bottomLeft"] = new { x = corners[2].X, y = corners[2].Y },
                    ["bottomRight"] = new { x = corners[3].X, y = corners[3].Y }
                };
                
                // Calculate grid dimensions
                int width = Math.Max(corners[1].X, corners[3].X) - Math.Min(corners[0].X, corners[2].X);
                int height = Math.Max(corners[2].Y, corners[3].Y) - Math.Min(corners[0].Y, corners[1].Y);
                result["gridDimensions"] = new { width, height };
                
                // Calculate aspect ratio
                double aspectRatio = (double)width / height;
                result["aspectRatio"] = aspectRatio;
            }
            else
            {
                result["success"] = false;
                result["cornerCount"] = corners.Count;
                result["error"] = $"Only found {corners.Count} corners, expected 4";
            }
            
            return result;
        }
        
        /// <summary>
        /// Draws detected corners on the image for debugging visualization.
        /// </summary>
        /// <param name="image">The image to draw corners on</param>
        /// <param name="originalImage">The original image for corner detection</param>
        private void DrawDetectedCorners(Image<Rgba32> image, Image<Rgba32> originalImage)
        {
            // Convert to grayscale for analysis
            using var grayscaleImage = originalImage.Clone();
            grayscaleImage.Mutate(x => x.Grayscale());
            
            // Apply edge detection to find grid lines
            using var edgeImage = grayscaleImage.Clone();
            edgeImage.Mutate(x => x.GaussianBlur(1.0f));
            ApplySobelEdgeDetection(edgeImage);
            
            // Find the four corners of the chess notation grid
            var corners = FindGridCorners(edgeImage, originalImage.Width, originalImage.Height);
            
            if (corners.Count >= 4)
            {
                // Sort corners to identify top-left, top-right, bottom-left, bottom-right
                var sortedCorners = SortCorners(corners);
                
                // Draw corners with different colors
                var colors = new Rgba32[]
                {
                    new Rgba32(255, 0, 0, 255),    // Red for top-left
                    new Rgba32(0, 255, 0, 255),    // Green for top-right
                    new Rgba32(0, 0, 255, 255),    // Blue for bottom-left
                    new Rgba32(255, 255, 0, 255)   // Yellow for bottom-right
                };
                
                string[] labels = { "TL", "TR", "BL", "BR" };
                
                for (int i = 0; i < Math.Min(4, sortedCorners.Count); i++)
                {
                    var corner = sortedCorners[i];
                    var color = colors[i];
                    
                    // Draw a circle around the corner
                    int radius = 15;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            int x = corner.X + dx;
                            int y = corner.Y + dy;
                            
                            if (x >= 0 && x < image.Width && y >= 0 && y < image.Height)
                            {
                                double distance = Math.Sqrt(dx * dx + dy * dy);
                                if (distance <= radius)
                                {
                                    image[x, y] = color;
                                }
                            }
                        }
                    }
                    
                    _logger.LogInformation($"Drew {labels[i]} corner at ({corner.X}, {corner.Y})");
                }
            }
            else
            {
                _logger.LogWarning($"Only found {corners.Count} corners, not drawing corner visualization");
            }
        }
    }

    /// <summary>
    /// Information about a detected column.
    /// </summary>
    public class ColumnInfo
    {
        public int Index { get; set; }
        public int StartX { get; set; }
        public int EndX { get; set; }
        public int Width { get; set; }
        public double RelativeWidth { get; set; }
    }

    /// <summary>
    /// Information about a sequence of columns being analyzed for chess moves.
    /// </summary>
    public class ColumnSequence
    {
        public List<ColumnInfo> Columns { get; set; } = new();
        public int StartX { get; set; }
        public int EndX { get; set; }
        public double AverageWidth { get; set; }
        public double WidthUniformity { get; set; }
        public double Centeredness { get; set; }
        public double SizeScore { get; set; }
        public double Score { get; set; }
    }
} 