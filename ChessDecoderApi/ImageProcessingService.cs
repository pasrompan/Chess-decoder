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
            { "Α", "B" }, // Αλογο (Knight)
            { "Β", "Q" }, // Βασίλισσα (Queen)
            { "Ι", "N" }, // Ιππος (Knight)
            { "Ρ", "K" }, // Ρήγας (King)
            { "0", "0" }, // Castling short
            { "O", "0" }, // Castling short
            { "x", "x" }, // Capture
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
        /// <param name="imagePath">Path to the chess image</param>
        /// <param name="language">Language for chess notation (default: English)</param>
        /// <returns>Tuple of two lists: whiteMoves and blackMoves</returns>
        public virtual async Task<(List<string> whiteMoves, List<string> blackMoves)> ExtractMovesFromImageToStringAsync(string imagePath, string language = "English")
        {
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found", imagePath);
            }

            // Load the image
            using var image = Image.Load<Rgba32>(imagePath);
            int width = image.Width;
            int height = image.Height;

            // Get column boundaries
            var boundaries = SplitImageIntoColumns(imagePath, 6);
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

                    // Validate moves and log any issues
                    var validationResult = _chessMoveValidator.ValidateMoves(moves);
                    foreach (var move in validationResult.Moves)
                    {
                        switch (move.ValidationStatus)
                        {
                            case "error":
                                _logger.LogError("Move validation error: Move {MoveNumber} '{Move}': {Error}", 
                                    move.MoveNumber, move.Notation, move.ValidationText);
                                break;
                            case "warning":
                                _logger.LogWarning("Move validation warning: Move {MoveNumber} '{Move}': {Warning}", 
                                    move.MoveNumber, move.Notation, move.ValidationText);
                                break;
                        }
                    }

                    _logger.LogInformation($"Successfully processed {moves.Length} moves from column {i}");
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
        /// <param name="imagePath">Path to the chess image</param>
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
                    model = "chatgpt-4o-latest",
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
                    "Π", "Α", "Β", "Ι", "Ρ", // Greek piece names
                    "0", "O", "x", "+", "#", // Special symbols
                    "α", "β", "γ", "δ", "ε", "ζ", "η", "θ", // Greek file letters
                    "1", "2", "3", "4", "5", "6", "7", "8", // Rank numbers
                },
                "English" => new[]
                {
                    "R", "N", "B", "Q", "K", // English piece names
                    "x", "+", "#", "0", "=", // Special symbols
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
                string white = i < whiteList.Count ? whiteList[i] : null;
                string black = i < blackList.Count ? blackList[i] : null;
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
                model = "chatgpt-4o-latest",
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
        /// <param name="imagePath">Path to the chess moves image</param>
        /// <param name="expectedColumns">Expected number of columns (default: 6)</param>
        /// <returns>List of column boundary indices (pixel positions)</returns>
        public List<int> SplitImageIntoColumns(string imagePath, int expectedColumns = 6)
        {
            using var image = Image.Load<Rgba32>(imagePath);
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
    }
} 