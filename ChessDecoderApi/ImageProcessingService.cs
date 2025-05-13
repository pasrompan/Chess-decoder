using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace ChessDecoderApi.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImageProcessingService> _logger;

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
            ILogger<ImageProcessingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> ProcessImageAsync(string imagePath)
        {
            // Check if file exists
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file not found", imagePath);
            }

            // Load the image
            using var image = await Image.LoadAsync(imagePath);

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

            // Extract text from the image using OpenAI
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
                _configuration["OPENAI_API_KEY"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogInformation("API Key available: {available}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
                    throw new UnauthorizedAccessException("OPENAI_API_KEY environment variable not set");}

    

            string language = "English"; // Default to English language
            string text = await ExtractTextFromImageAsync(imageBytes, language);

            // Convert the extracted text to PGN format
            string[] moves;
            if (language == "Greek")
            {
                moves = await ConvertGreekMovesToEnglishAsync(text.Split('\n'));
            }
            else
            {
                moves = text.Split('\n');
            }

            // Generate the PGN content
            return await GeneratePGNContentAsync(moves);
        }

        public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string language)
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
                var promptText = "You are an OCR engine. Transcribe all visible chess moves from this image exactly as they appear, but only include characters that are valid in a chess game. The valid characters are: ";
                
                // Add each valid character to the prompt
                for (int i = 0; i < validChars.Length; i++)
                {
                    if (i > 0)
                    {
                        promptText += ", ";
                    }
                    promptText += validChars[i];
                }
                
                promptText += ". Do not include any other characters, and preserve any misspellings, punctuation, or line breaks. Return only the raw text with one move per line.";

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
                    max_tokens = 300
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image");
                throw;
            }
        }

        public async Task<string> ExtractTextFromImageWithGoogleVisionAsync(byte[] imageBytes, string language)
        {
            try
            {
                // Get Google Cloud Vision API key from environment or configuration
                string apiKey = Environment.GetEnvironmentVariable("GOOGLE_VISION_API_KEY") ?? 
                    _configuration["GOOGLE_VISION_API_KEY"] ?? 
                    throw new UnauthorizedAccessException("GOOGLE_VISION_API_KEY not set");

                var client = _httpClientFactory.CreateClient();
                
                // Convert image to base64 encoding
                var base64Image = Convert.ToBase64String(imageBytes);
                
                // Prepare the Google Vision API request
                var requestData = new
                {
                    requests = new[]
                    {
                        new
                        {
                            image = new
                            {
                                content = base64Image
                            },
                            features = new[]
                            {
                                new
                                {
                                    type = "TEXT_DETECTION",
                                    maxResults = 1
                                }
                            },
                            imageContext = new
                            {
                                languageHints = new[] {  "en-t-i0-handwrit"  }
                            }
                        }
                    }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json");

                // Send request to Google Vision API
                var response = await client.PostAsync(
                    $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}", 
                    content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Google Vision API error: {response.StatusCode} - {errorContent}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseContent);
                
                // Extract OCR text from the response
                var text = jsonDoc.RootElement
                    .GetProperty("responses")[0]
                    .TryGetProperty("fullTextAnnotation", out var textAnnotation) 
                        ? textAnnotation.GetProperty("text").GetString() 
                        : string.Empty;

                if (string.IsNullOrEmpty(text))
                {
                    return string.Empty;
                }

                // Get valid characters for the specified language
                var validChars = GetChessNotationCharacters(language);
                var validCharsSet = new HashSet<char>(validChars.SelectMany(c => c));
                
                // Filter out invalid characters and process the text
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var filteredLines = new List<string>();
                
                foreach (var line in lines)
                {
                    // Keep only valid characters and basic formatting
                    var filteredLine = new string(line.Where(c => 
                        validCharsSet.Contains(c) || 
                        c == ' ' || 
                        c == '.' || 
                        c == '-').ToArray());
                    
                    // Only add non-empty lines after filtering
                    if (!string.IsNullOrWhiteSpace(filteredLine))
                    {
                        filteredLines.Add(filteredLine.Trim());
                    }
                }
                
                return string.Join("\n", filteredLines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image with Google Vision");
                throw;
            }
        }

        private async Task<string[]> ConvertGreekMovesToEnglishAsync(string[] greekMoves)
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

            return englishMoves;
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
            await Task.Yield(); // Makes the method truly asynchronous
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
            using var image = await Image.LoadAsync(imagePath);
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
                max_tokens = 300
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