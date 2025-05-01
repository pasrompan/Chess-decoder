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
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? 
                _configuration["OPENAI_API_KEY"] ?? 
                throw new UnauthorizedAccessException("OPENAI_API_KEY environment variable not set");

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
                                    text = $"Extract the chess moves from this image. Return just the moves in standard notation, one per line."
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
                
                return messageContent ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from image");
                throw;
            }
        }

        private async Task<string[]> ConvertGreekMovesToEnglishAsync(string[] greekMoves)
        {
            await Task.Yield(); // Makes the method truly asynchronous
            // Rest of the implementation
            return greekMoves;
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
    }
}