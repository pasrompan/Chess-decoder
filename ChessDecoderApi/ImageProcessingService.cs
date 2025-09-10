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
        /// Extracts chess moves from an image and returns two lists: white and black moves.
        /// </summary>
        /// <param name="imagePath">Path to the chess image or URL</param>
        /// <param name="language">Language for chess notation (default: English)</param>
        /// <returns>Tuple of two lists: whiteMoves and blackMoves</returns>
        public virtual async Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(string imagePath, string language = "English")
        {
            Image<Rgba32> image;
            
            // Check if it's a URL or local file path
            if (Uri.TryCreate(imagePath, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                // Download image from URL
                using var httpClient = _httpClientFactory.CreateClient();
                var imageBytes = await httpClient.GetByteArrayAsync(imagePath);
                image = Image.Load<Rgba32>(imageBytes);
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
            int width = image.Width;
            int height = image.Height;

            // Get column boundaries - we need to pass the image directly since we already loaded it
            var boundaries = SplitImageIntoColumns(image, 6);
            var whiteMoves = new List<string>();
            var blackMoves = new List<string>();

            for (int i = 0; i < boundaries.Count - 1; i++)
            {
                int colStart = boundaries[i];
                int colEnd = boundaries[i + 1];
                int colWidth = colEnd - colStart;
                if (colWidth <= 0) continue;
                var rect = new Rectangle(colStart, 0, colWidth, height);
                using var columnImage = image.Clone(ctx => ctx.Crop(rect));
                byte[] imageBytes;
                using (var ms = new MemoryStream())
                {
                    columnImage.SaveAsJpeg(ms);
                    imageBytes = ms.ToArray();
                }
                string text = await ExtractTextFromImageAsync(imageBytes, language);
                string[] moves;
                try
                {
                    if (language == "Greek")
                    {
                        _logger.LogInformation($"Processing Greek chess notation for column {i}");
                        string[] greekMoves = await _chessMoveProcessor.ProcessChessMovesAsync(text);
                        moves = await ConvertGreekMovesToEnglishAsync(greekMoves);
                    }
                    else
                    {
                        _logger.LogInformation($"Processing standard chess notation for column {i}");
                        moves = await _chessMoveProcessor.ProcessChessMovesAsync(text);
                    }

                    if (moves == null || moves.Length == 0)
                    {
                        _logger.LogWarning($"No valid moves were extracted from column {i}");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing chess moves for column {i} and language: {language}");
                    continue;
                }

                // Merge moves into white and black lists
                if (i % 2 == 0)
                {
                    whiteMoves.AddRange(moves);
                }
                else
                {
                    blackMoves.AddRange(moves);
                }
            }

            return (whiteMoves, blackMoves);
        }

        /// <summary>
        /// Processes a chess image and returns the moves as a PGN string
        /// </summary>
        /// <param name="imagePath">Path to the chess image or URL</param>
        /// <param name="language">Language for chess notation (default: English)</param>
        /// <returns>PGN formatted string containing the chess moves</returns>
        public async Task<ChessGameResponse> ProcessImageAsync(string imagePath, string language = "English")
        {
            // Extract moves from the image
            var (whiteMoves, blackMoves) = await ExtractMovesFromImageToStringAsync(imagePath, language);

            // Validate white and black moves separately
            var whiteValidation = _chessMoveValidator.ValidateMoves(whiteMoves.ToArray());
            var blackValidation = _chessMoveValidator.ValidateMoves(blackMoves.ToArray());

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

            // Construct ChessMovePair list
            var validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            };
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
            }

            // Generate the PGN content
            var pgnContent = GeneratePGNContentAsync(whiteMoves, blackMoves);

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

        public virtual async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image");
                throw;
            }
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

        public string GeneratePGNContentAsync(IEnumerable<string> whiteMoves, IEnumerable<string> blackMoves)
        {
            // Basic PGN structure
            var sb = new StringBuilder();
            //sb.AppendLine("[Event \"??\"]");
            //sb.AppendLine("[Site \"??\"]");
            sb.AppendLine($"[Date \"{DateTime.Now:yyyy.MM.dd}\"]");
            //sb.AppendLine("[Round \"??\"]");
            sb.AppendLine("[White \"??\"]");
            sb.AppendLine("[Black \"??\"]");
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

            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
                _configuration["OPENAI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("OPENAI_API_KEY environment variable not set");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
            var base64Image = Convert.ToBase64String(imageBytes);

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
            
            return messageContent?.Replace("`", "") ?? string.Empty;
        }

        /// <summary>
        /// Splits the input image into vertical columns based on projection profile and returns the column boundaries.
        /// </summary>
        /// <param name="image">The chess moves image</param>
        /// <param name="expectedColumns">Expected number of columns (default: 6)</param>
        /// <returns>List of column boundary indices (pixel positions)</returns>
        public List<int> SplitImageIntoColumns(Image<Rgba32> image, int expectedColumns = 6)
        {
            image.Mutate(x => x.Grayscale());
            int width = image.Width;
            int height = image.Height;
            double[] columnSums = new double[width];
            // Binarize and sum binary values per column
            for (int x = 0; x < width; x++)
            {
                double sum = 0;
                for (int y = 0; y < height; y++)
                {
                    var pixel = image[x, y];
                    int gray = pixel.R;
                    int binary = gray > 128 ? 1 : 0; // Threshold at 128
                    sum += binary;
                }
                columnSums[x] = sum;
            }
            int smoothWindow = Math.Max(3, width / 100);
            double[] smoothed = new double[width];
            for (int x = 0; x < width; x++)
            {
                int start = Math.Max(0, x - smoothWindow);
                int end = Math.Min(width - 1, x + smoothWindow);
                double avg = 0;
                for (int i = start; i <= end; i++) avg += columnSums[i];
                smoothed[x] = avg / (end - start + 1);
            }
            // Detect boundaries as before
            List<int> detectedBoundaries = new List<int> { 0 };
            double threshold = 3.0;
            for (int x = 1; x < width - 1; x++)
            {
                double diff = Math.Abs(smoothed[x] - smoothed[x - 1]) + Math.Abs(smoothed[x] - smoothed[x + 1]);
                bool bothNeighborsAreNegative = (smoothed[x] - smoothed[x - 1] < 0 ) && (smoothed[x] - smoothed[x + 1] < 0);
                if (diff > threshold && bothNeighborsAreNegative)
                {
                    detectedBoundaries.Add(x);
                }
            }
            detectedBoundaries.Add(width);
            detectedBoundaries = detectedBoundaries.Distinct().OrderBy(b => b).ToList();

            // Step 1: Create dummy slices (width/expectedColumns)
            List<int> dummyEdges = new List<int>();
            for (int i = 0; i <= expectedColumns; i++)
            {
                dummyEdges.Add(i * width / expectedColumns);
            }

            // Step 2: For each dummy edge (except 0 and width), adjust to closest detected boundary if within range
            int maxAdjust = width / (expectedColumns * 2); // e.g., width/12 for 6 columns
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
            finalEdges.Add(width);

            // Ensure edges are sorted and unique, and at least 1 pixel apart
            finalEdges = finalEdges.Distinct().OrderBy(e => e).ToList();
            for (int i = finalEdges.Count - 1; i > 0; i--)
            {
                if (finalEdges[i] - finalEdges[i - 1] < 1)
                {
                    finalEdges.RemoveAt(i);
                }
            }

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
        /// Splits the input image into horizontal rows based on projection profile and returns the row boundaries.
        /// </summary>
        /// <param name="image">The chess moves image</param>
        /// <param name="expectedRows">Expected number of rows (default: 0 for auto-detection)</param>
        /// <returns>List of row boundary indices (pixel positions)</returns>
        public List<int> SplitImageIntoRows(Image<Rgba32> image, int expectedRows = 20)
        {
            image.Mutate(x => x.Grayscale());
            int width = image.Width;
            int height = image.Height;
            
            _logger.LogInformation($"Starting row detection for image {width}x{height}");
            
            // Calculate horizontal projection profile (sum of dark pixels per row)
            double[] horizontalProfile = new double[height];
            for (int y = 0; y < height; y++)
            {
                double sum = 0;
                for (int x = 0; x < width; x++)
                {
                    var pixel = image[x, y];
                    int gray = pixel.R;
                    // Invert threshold - look for dark content (text)
                    int binary = gray < 200 ? 1 : 0;
                    sum += binary;
                }
                horizontalProfile[y] = sum;
            }
            
            // Smooth the profile to reduce noise
            int smoothWindow = Math.Max(5, height / 50);
            double[] smoothed = SmoothProfile(horizontalProfile);
            
            // Find valleys (gaps between text rows) using derivative analysis
            List<int> valleys = new List<int>();
            
            // Calculate first derivative to find peaks and valleys
            double[] derivative = new double[height - 1];
            for (int y = 0; y < height - 1; y++)
            {
                derivative[y] = smoothed[y + 1] - smoothed[y];
            }
            
            // Find valleys where derivative changes from negative to positive
            for (int y = 1; y < derivative.Length - 1; y++)
            {
                if (derivative[y - 1] < 0 && derivative[y] > 0 && derivative[y + 1] > 0)
                {
                    // This is a valley - potential row boundary
                    valleys.Add(y);
                }
            }
            
            _logger.LogInformation($"Found {valleys.Count} potential valleys");
            
            // Filter valleys based on minimum row height
            int minRowHeight = Math.Max(15, height / 30); // At least 15px or 3.3% of image height
            List<int> filteredValleys = new List<int>();
            
            for (int i = 0; i < valleys.Count; i++)
            {
                int currentValley = valleys[i];
                
                // Check if this valley is far enough from the previous one
                if (filteredValleys.Count == 0 || currentValley - filteredValleys.Last() >= minRowHeight)
                {
                    // Also check if there's enough content above this valley
                    int contentAbove = 0;
                    int startY = filteredValleys.Count == 0 ? 0 : filteredValleys.Last();
                    for (int y = startY; y < currentValley; y++)
                    {
                        if (smoothed[y] > smoothed.Average() * 0.3) // Some content threshold
                        {
                            contentAbove++;
                        }
                    }
                    
                    if (contentAbove >= minRowHeight / 3) // At least some content above
                    {
                        filteredValleys.Add(currentValley);
                    }
                }
            }
            
            _logger.LogInformation($"Filtered to {filteredValleys.Count} row boundaries (min height: {minRowHeight}px)");
            
            // If we didn't find enough valleys, try a different approach
            if (filteredValleys.Count < 3)
            {
                _logger.LogInformation("Not enough valleys found, trying alternative approach...");
                
                // Alternative: Find rows by looking for consistent text patterns
                List<int> alternativeRows = FindRowsByTextPatterns(smoothed, height, minRowHeight);
                if (alternativeRows.Count > filteredValleys.Count)
                {
                    _logger.LogInformation($"Alternative approach found {alternativeRows.Count} rows");
                    return alternativeRows;
                }
            }
            
            // Build final boundaries
            List<int> boundaries = new List<int> { 0 };
            boundaries.AddRange(filteredValleys);
            boundaries.Add(height);
            
            // Remove duplicates and sort
            boundaries = boundaries.Distinct().OrderBy(b => b).ToList();
            
            // Ensure minimum spacing between boundaries
            for (int i = boundaries.Count - 1; i > 0; i--)
            {
                if (boundaries[i] - boundaries[i - 1] < minRowHeight / 2)
                {
                    boundaries.RemoveAt(i);
                }
            }
            
            _logger.LogInformation($"Final row boundaries: {boundaries.Count - 1} rows");
            return boundaries;
        }
        
        /// <summary>
        /// Alternative method to find rows by analyzing text patterns
        /// </summary>
        private List<int> FindRowsByTextPatterns(double[] profile, int height, int minRowHeight)
        {
            List<int> boundaries = new List<int> { 0 };
            
            // Calculate statistics
            double maxValue = profile.Max();
            double avgValue = profile.Average();
            double threshold = avgValue * 0.4; // Lower threshold for text detection
            
            _logger.LogInformation($"Text pattern detection - Max: {maxValue:F2}, Avg: {avgValue:F2}, Threshold: {threshold:F2}");
            
            bool inTextRow = false;
            int textRowStart = 0;
            
            for (int y = 0; y < height; y++)
            {
                if (profile[y] > threshold)
                {
                    if (!inTextRow)
                    {
                        // Starting a new text row
                        inTextRow = true;
                        textRowStart = y;
                    }
                }
                else
                {
                    if (inTextRow)
                    {
                        // Ending a text row
                        inTextRow = false;
                        int textRowEnd = y;
                        int textRowHeight = textRowEnd - textRowStart;
                        
                        if (textRowHeight >= minRowHeight / 2) // Minimum text height
                        {
                            // Add boundary at the end of this text row
                            boundaries.Add(textRowEnd);
                        }
                    }
                }
            }
            
            // If we ended in a text row, add the final boundary
            if (inTextRow)
            {
                boundaries.Add(height);
            }
            
            boundaries.Add(height);
            return boundaries.Distinct().OrderBy(b => b).ToList();
        }

        /// <summary>
        /// Splits the input image into horizontal rows based on projection profile and returns the row boundaries.
        /// </summary>
        /// <param name="imagePath">Path to the chess moves image</param>
        /// <param name="expectedRows">Expected number of rows (default: 0 for auto-detection)</param>
        /// <returns>List of row boundary indices (pixel positions)</returns>
        public List<int> SplitImageIntoRows(string imagePath, int expectedRows = 0)
        {
            using var image = Image.Load<Rgba32>(imagePath);
            return SplitImageIntoRows(image, expectedRows);
        }

        /// <summary>
        /// Creates an image with table boundaries drawn on it for debugging visualization.
        /// </summary>
        /// <param name="imagePath">Path to the chess moves image</param>
        /// <param name="expectedColumns">Expected number of columns (default: 6)</param>
        /// <param name="expectedRows">Expected number of rows (default: 8)</param>
        /// <returns>Byte array of the image with table boundaries drawn</returns>
        public async Task<byte[]> CreateImageWithBoundariesAsync(string imagePath, int expectedColumns = 6, int expectedRows = 8)
        {
            using var image = Image.Load<Rgba32>(imagePath);
            var tableBoundaries = FindTableBoundaries(image);
            
            // Clone the image to draw on
            using var imageWithBoundaries = image.Clone();
            
            // Draw only table boundaries (blue, thicker) for debugging
            DrawTableBoundariesOnImage(imageWithBoundaries, tableBoundaries);
            
            // Convert to byte array
            using var ms = new MemoryStream();
            await imageWithBoundaries.SaveAsJpegAsync(ms);
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
            
            // Group text blocks into rows and columns
            var groupedBlocks = GroupTextBlocksIntoTable(textBlocks);
            
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
            
            _logger.LogInformation("Starting corner detection using edge analysis");
            
            // Find horizontal and vertical lines
            var horizontalLines = FindHorizontalLines(edgeImage, width, height);
            var verticalLines = FindVerticalLines(edgeImage, width, height);
            
            _logger.LogInformation($"Found {horizontalLines.Count} horizontal lines and {verticalLines.Count} vertical lines");
            
            // Find intersections between horizontal and vertical lines
            var intersections = FindLineIntersections(horizontalLines, verticalLines);
            
            _logger.LogInformation($"Found {intersections.Count} line intersections");
            
            // Filter intersections to find the most likely grid corners
            corners = FilterGridCorners(intersections, width, height);
            
            _logger.LogInformation($"Filtered to {corners.Count} potential grid corners");
            
            return corners;
        }
        
        /// <summary>
        /// Finds horizontal lines in the edge-detected image.
        /// </summary>
        private List<Line> FindHorizontalLines(Image<Rgba32> edgeImage, int width, int height)
        {
            var lines = new List<Line>();
            int minLineLength = width / 4; // Minimum line length as fraction of image width
            int lineThickness = 3; // Allow for some thickness in line detection
            
            // Scan horizontally for lines
            for (int y = 0; y < height; y++)
            {
                int lineStart = -1;
                int consecutivePixels = 0;
                
                for (int x = 0; x < width; x++)
                {
                    var pixel = edgeImage[x, y];
                    bool isEdge = pixel.R > 128; // Edge threshold
                    
                    if (isEdge)
                    {
                        if (lineStart == -1)
                        {
                            lineStart = x;
                        }
                        consecutivePixels++;
                    }
                    else
                    {
                        if (consecutivePixels >= minLineLength)
                        {
                            // Found a horizontal line
                            lines.Add(new Line { Start = new Point(lineStart, y), End = new Point(x - 1, y) });
                        }
                        lineStart = -1;
                        consecutivePixels = 0;
                    }
                }
                
                // Check if line extends to end of image
                if (consecutivePixels >= minLineLength)
                {
                    lines.Add(new Line { Start = new Point(lineStart, y), End = new Point(width - 1, y) });
                }
            }
            
            // Merge nearby horizontal lines (within lineThickness pixels)
            return MergeNearbyLines(lines, lineThickness, true);
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
            return MergeNearbyLines(lines, lineThickness, false);
        }
        
        /// <summary>
        /// Merges nearby lines to reduce noise and duplicate detections.
        /// </summary>
        private List<Line> MergeNearbyLines(List<Line> lines, int threshold, bool isHorizontal)
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
                    
                    // Check if lines are close enough to merge
                    bool shouldMerge = false;
                    if (isHorizontal)
                    {
                        // For horizontal lines, check if Y coordinates are close
                        shouldMerge = Math.Abs(currentLine.Start.Y - otherLine.Start.Y) <= threshold;
                    }
                    else
                    {
                        // For vertical lines, check if X coordinates are close
                        shouldMerge = Math.Abs(currentLine.Start.X - otherLine.Start.X) <= threshold;
                    }
                    
                    if (shouldMerge)
                    {
                        // Merge the lines by extending the current line
                        if (isHorizontal)
                        {
                            mergedLine.Start = new Point(Math.Min(mergedLine.Start.X, otherLine.Start.X), mergedLine.Start.Y);
                            mergedLine.End = new Point(Math.Max(mergedLine.End.X, otherLine.End.X), mergedLine.End.Y);
                        }
                        else
                        {
                            mergedLine.Start = new Point(mergedLine.Start.X, Math.Min(mergedLine.Start.Y, otherLine.Start.Y));
                            mergedLine.End = new Point(mergedLine.End.X, Math.Max(mergedLine.End.Y, otherLine.End.Y));
                        }
                        used[j] = true;
                    }
                }
                
                mergedLines.Add(mergedLine);
            }
            
            return mergedLines;
        }
        
        /// <summary>
        /// Finds intersections between horizontal and vertical lines.
        /// </summary>
        private List<Point> FindLineIntersections(List<Line> horizontalLines, List<Line> verticalLines)
        {
            var intersections = new List<Point>();
            
            foreach (var hLine in horizontalLines)
            {
                foreach (var vLine in verticalLines)
                {
                    var intersection = FindLineIntersection(hLine, vLine);
                    if (intersection.HasValue)
                    {
                        intersections.Add(intersection.Value);
                    }
                }
            }
            
            return intersections;
        }
        
        /// <summary>
        /// Finds the intersection point between a horizontal and vertical line.
        /// </summary>
        private Point? FindLineIntersection(Line hLine, Line vLine)
        {
            // Check if the vertical line's X is within the horizontal line's X range
            if (vLine.Start.X < hLine.Start.X || vLine.Start.X > hLine.End.X)
                return null;
            
            // Check if the horizontal line's Y is within the vertical line's Y range
            if (hLine.Start.Y < vLine.Start.Y || hLine.Start.Y > vLine.End.Y)
                return null;
            
            return new Point(vLine.Start.X, hLine.Start.Y);
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
        /// Groups text blocks into table structure.
        /// </summary>
        private List<List<Rectangle>> GroupTextBlocksIntoTable(List<Rectangle> textBlocks)
        {
            // Simple grouping by Y coordinate (rows)
            var rows = textBlocks
                .GroupBy(block => block.Y / 20) // Group by approximate row
                .OrderBy(g => g.Key)
                .Select(g => g.OrderBy(b => b.X).ToList())
                .ToList();
            
            return rows;
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
        /// Draws row boundaries on the image for visualization.
        /// </summary>
        /// <param name="image">The image to draw boundaries on</param>
        /// <param name="boundaries">List of row boundary positions</param>
        private void DrawRowBoundariesOnImage(Image<Rgba32> image, List<int> boundaries)
        {
            int width = image.Width;
            var blue = new Rgba32(0, 0, 255, 255); // Blue color
            
            // Draw horizontal lines for each boundary (except the first and last which are edges)
            for (int i = 1; i < boundaries.Count - 1; i++)
            {
                int y = boundaries[i];
                
                // Draw thick horizontal line (5-pixel wide for better visibility)
                for (int x = 0; x < width; x++)
                {
                    // Draw 5-pixel wide line
                    for (int offset = -2; offset <= 2; offset++)
                    {
                        int lineY = y + offset;
                        if (lineY >= 0 && lineY < image.Height)
                        {
                            image[x, lineY] = blue;
                        }
                    }
                }
            }
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
} 