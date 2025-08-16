using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ChessDecoderApi.Services
{
    public class ChessMoveProcessor : IChessMoveProcessor
    {
        private readonly ILogger<ChessMoveProcessor> _logger;

        public ChessMoveProcessor(ILogger<ChessMoveProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<string[]> ProcessChessMovesAsync(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                _logger.LogWarning("Received empty or whitespace input for processing");
                return Task.FromResult(Array.Empty<string>());
            }

            try
            {
                _logger.LogDebug("Attempting to parse chess moves from input");
                
                // Find the content after "json" and newline
                rawText = rawText.Trim('`');
                var jsonStartIndex = rawText.IndexOf("json\n");
                if (jsonStartIndex == -1)
                {
                    jsonStartIndex = rawText.IndexOf("json\r\n");
                }

                var jsonPart = string.Empty;

                // Extract the JSON array part after the "json" indicator and newline
                if (jsonStartIndex > -1)
                {
                    jsonPart = rawText.Substring(jsonStartIndex + 5); // 5 is length of "json\n"
                }
                else
                {
                    jsonPart = rawText;
                }
                
                
                // Parse the inner JSON array
                var moves = JsonSerializer.Deserialize<string[]>(jsonPart);

                if (moves == null)
                {
                    _logger.LogError("Failed to deserialize chess moves - null result");
                    throw new ArgumentException("Failed to deserialize chess moves - null result");
                }

                _logger.LogInformation("Successfully processed {MoveCount} chess moves", moves.Length);
                _logger.LogDebug("Processed moves: {Moves}", string.Join(", ", moves));

                return Task.FromResult(moves);
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
        public string? response { get; set; }
    }
} 