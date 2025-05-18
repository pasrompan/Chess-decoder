using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ChessDecoderApi.Services
{
    public class ChessMoveProcessor
    {
        private readonly ILogger<ChessMoveProcessor> _logger;

        public ChessMoveProcessor(ILogger<ChessMoveProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string[]> ProcessChessMovesAsync(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning("Received empty or whitespace input for processing");
                return Array.Empty<string>();
            }

            try
            {
                _logger.LogDebug("Attempting to parse chess moves from input");
                
                // Find the content after "json" and newline
                rawText = rawText.Trim('`');
                var jsonStartIndex = rawText.IndexOf("json\n");
                if (jsonStartIndex == -1)
                {
                    _logger.LogError("Invalid response format: 'json' indicator not found");
                    throw new ArgumentException("Invalid response format: 'json' indicator not found");
                }

                // Extract the JSON array part after the "json" indicator and newline
                var jsonPart = rawText.Substring(jsonStartIndex + 5); // 5 is length of "json\n"
                
                // Parse the inner JSON array
                var moves = JsonSerializer.Deserialize<string[]>(jsonPart);

                _logger.LogInformation("Successfully processed {MoveCount} chess moves", moves.Length);
                _logger.LogDebug("Processed moves: {Moves}", string.Join(", ", moves));

                return moves;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse chess moves response as JSON");
                throw new ArgumentException("Failed to parse chess moves response as JSON", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing chess moves");
                throw;
            }
        }
    }

    public class OuterResponse
    {
        public string response { get; set; }
    }
} 