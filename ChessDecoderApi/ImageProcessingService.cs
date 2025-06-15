using ChessDecoderApi.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
        /// Extracts chess moves from an image and returns them as a list of strings
        /// </summary>
        /// <param name="imagePath">Path to the chess image</param>
        /// <param name="language">Language for chess notation (default: English)</param>
        /// <returns>Array of chess moves in standard notation</returns>
        public virtual async Task<string[]> ExtractMovesFromImageToStringAsync(string imagePath, string language = "English")
        {
            // Check if file exists
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found", imagePath);
            }

            // Load and process the image
            byte[] imageBytes = await LoadAndProcessImageAsync(imagePath);

            // // Extract text from the image using OpenAI
            // var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
            //     _configuration["OPENAI_API_KEY"];
            //     if (string.IsNullOrEmpty(apiKey))
            //     {
            //         _logger.LogInformation("API Key available: {available}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
            //         throw new UnauthorizedAccessException("OPENAI_API_KEY environment variable not set");}

            string text = await ExtractTextFromImageAsync(imageBytes, language);

            // Convert the extracted text to moves
            string[] moves;
            try
            {
                if (language == "Greek")
                {
                    _logger.LogInformation("Processing Greek chess notation");
                    string[] greekMoves = await _chessMoveProcessor.ProcessChessMovesAsync(text);
                    moves = await ConvertGreekMovesToEnglishAsync(greekMoves);
                }
                else
                {
                    _logger.LogInformation("Processing standard chess notation");
                    moves = await _chessMoveProcessor.ProcessChessMovesAsync(text);
                }

                if (moves == null || moves.Length == 0)
                {
                    _logger.LogWarning("No valid moves were extracted from the image");
                    throw new InvalidOperationException("No valid moves were extracted from the image");
                }

                
                _logger.LogInformation("Successfully processed {MoveCount} moves", moves.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chess moves for language: {Language}", language);
                throw;
            }

            return moves;
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
            string[] moves = await ExtractMovesFromImageToStringAsync(imagePath, language);
            // Validate moves and log any issues
                ChessMoveValidationResult validationResult;
                validationResult = _chessMoveValidator.ValidateMoves(moves);
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
                
                 // Convert validation result to the new format
            var validation = new ChessGameValidation
            {
                GameId = Guid.NewGuid().ToString(),
                Moves = new List<ChessMovePair>()
            };

            // Group moves into pairs (white and black moves)
            for (int i = 0; i < validationResult.Moves.Count; i += 2)
            {
                var movePair = new ChessMovePair
                {
                    MoveNumber = (i / 2) + 1,
                    WhiteMove = new Models.ValidatedMove
                    {
                        Notation = validationResult.Moves[i].Notation,
                        NormalizedNotation = validationResult.Moves[i].NormalizedNotation,
                        ValidationStatus = validationResult.Moves[i].ValidationStatus,
                        ValidationText = validationResult.Moves[i].ValidationText
                    }
                };

                // Add black move if it exists
                if (i + 1 < validationResult.Moves.Count)
                {
                    movePair.BlackMove = new Models.ValidatedMove
                    {
                        Notation = validationResult.Moves[i + 1].Notation,
                        NormalizedNotation = validationResult.Moves[i + 1].NormalizedNotation,
                        ValidationStatus = validationResult.Moves[i + 1].ValidationStatus,
                        ValidationText = validationResult.Moves[i + 1].ValidationText
                    };
                }

                validation.Moves.Add(movePair);
            }


            // Generate the PGN content
            var pgnContent = await GeneratePGNContentAsync(moves);

           

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
        
        public async Task<string> GeneratePGNContentAsync(IEnumerable<string> moves)
        {
            // Basic PGN structure
            var sb = new StringBuilder();
            sb.AppendLine("[Event \"??\"]");
            sb.AppendLine("[Site \"??\"]");
            sb.AppendLine("[Date \"??\"]");
            sb.AppendLine("[Round \"??\"]");
            sb.AppendLine("[White \"??\"]");
            sb.AppendLine("[Black \"??\"]");
            sb.AppendLine("[Result \"*\"]");
            sb.AppendLine();

            // Format moves
            var moveList = new List<string>();
            int moveNumber = 1;
            bool whiteMove = true;

            foreach (var move in moves.Where(m => !string.IsNullOrWhiteSpace(m)))
            {
                if (whiteMove)
                {
                    moveList.Add($"{moveNumber}. {move}");
                    whiteMove = false;
                }
                else
                {
                    moveList.Add(move);
                    whiteMove = true;
                    moveNumber++;
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
    }
} 