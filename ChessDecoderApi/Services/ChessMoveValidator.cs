using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ChessDecoderApi.Services
{
    public class ChessMoveValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Suggestions { get; set; } = new();
        public string[]? NormalizedMoves { get; set; }
    }

    public class ChessMoveValidator
    {
        private readonly ILogger<ChessMoveValidator> _logger;
        private static readonly Regex _validMovePattern = new(@"^([KQRBN]?[a-h]?[1-8]?x?[a-h][1-8](=[QRBN])?[+#]?|O-O(-O)?[+#]?)$", RegexOptions.Compiled);
        private static readonly HashSet<string> _validPieces = new() { "K", "Q", "R", "B", "N" };
        private static readonly HashSet<string> _validPromotions = new() { "=Q", "=R", "=B", "=N" };
        private static readonly Regex _castlingPattern = new(@"^[0Oo]-[0Oo](-[0Oo])?[+#]?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ChessMoveValidator(ILogger<ChessMoveValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ChessMoveValidationResult ValidateMoves(string[] moves)
        {
            var result = new ChessMoveValidationResult { IsValid = true };

            if (moves == null || moves.Length == 0)
            {
                result.IsValid = false;
                result.Errors.Add("No moves provided for validation");
                return result;
            }

            // Create a copy of moves for normalization
            result.NormalizedMoves = new string[moves.Length];

            for (int i = 0; i < moves.Length; i++)
            {
                var move = moves[i].Trim();
                var normalizedMove = NormalizeCastling(move);
                result.NormalizedMoves[i] = normalizedMove;

                var moveValidation = ValidateSingleMove(normalizedMove, i + 1);
                
                if (!moveValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(moveValidation.Errors);
                }
                
                result.Warnings.AddRange(moveValidation.Warnings);
                result.Suggestions.AddRange(moveValidation.Suggestions);
            }

            // Additional game-level validations
            ValidateGameLevelRules(result.NormalizedMoves, result);

            return result;
        }

        private string NormalizeCastling(string move)
        {
            if (_castlingPattern.IsMatch(move))
            {
                // Check if it's queenside castling (has two hyphens)
                if (move.Count(c => c == '-') == 2)
                {
                    return "O-O-O" + (move.EndsWith("+") ? "+" : move.EndsWith("#") ? "#" : "");
                }
                // Otherwise it's kingside castling
                return "O-O" + (move.EndsWith("+") ? "+" : move.EndsWith("#") ? "#" : "");
            }
            return move;
        }

        private ChessMoveValidationResult ValidateSingleMove(string move, int moveNumber)
        {
            var result = new ChessMoveValidationResult { IsValid = true };

            // Check for empty or whitespace moves
            if (string.IsNullOrWhiteSpace(move))
            {
                result.IsValid = false;
                result.Errors.Add($"Move {moveNumber}: Empty or whitespace move");
                return result;
            }

            // Basic syntax validation
            if (!_validMovePattern.IsMatch(move))
            {
                result.IsValid = false;
                result.Errors.Add($"Move {moveNumber}: Invalid move syntax '{move}'");
                
                // Only suggest for invalid promotion pieces now
                if (move.Contains("=") && !_validPromotions.Any(p => move.EndsWith(p)))
                {
                    result.Suggestions.Add($"Move {moveNumber}: Invalid promotion piece. Valid promotions are: =Q, =R, =B, =N");
                }
                return result;
            }

            // Skip piece validation for castling moves
            if (!_castlingPattern.IsMatch(move))
            {
                // Validate piece notation only for non-castling moves
                if (move.Length > 1 && char.IsUpper(move[0]))
                {
                    var piece = move[0].ToString();
                    if (!_validPieces.Contains(piece))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Move {moveNumber}: Invalid piece notation '{piece}'");
                        return result;
                    }
                }
            }

            return result;
        }

        private void ValidateGameLevelRules(string[] moves, ChessMoveValidationResult result)
        {
            // Check for consecutive checks
            for (int i = 0; i < moves.Length - 1; i++)
            {
                var currentMove = moves[i];
                var nextMove = moves[i + 1];

                if (currentMove.EndsWith("+") && nextMove.EndsWith("+"))
                {
                    result.Warnings.Add($"Moves {i + 1}-{i + 2}: Consecutive checks detected. Please verify these moves.");
                }
            }
        }
    }
} 