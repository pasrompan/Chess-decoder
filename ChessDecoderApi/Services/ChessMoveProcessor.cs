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
                
                // Parse the JSON response
                var response = JsonSerializer.Deserialize<ChessResponse>(rawText);
                
                if (response?.response == null)
                {
                    _logger.LogError("Invalid response format: Response property is null");
                    throw new ArgumentException("Invalid response format: Response property is null");
                }

                // Split the response into moves and clean them
                var moves = response.response
                    .Split('\n')
                    .SelectMany(line => line.Split(','))
                    .Select(move => move.Trim().Trim('"', ' ', '[', ']'))
                    .Where(move => !string.IsNullOrWhiteSpace(move))
                    .ToArray();

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

    public class ChessResponse
    {
        public string response { get; set; }
    }
} 