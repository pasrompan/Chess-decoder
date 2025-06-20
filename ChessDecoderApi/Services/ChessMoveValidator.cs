using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace ChessDecoderApi.Services
{
    /// <summary>
    /// Represents a validated chess move with its original and normalized notation.
    /// This class maintains both the original notation (as detected from input) and a normalized version
    /// to support different use cases:
    /// - Original notation for debugging and showing users what was actually detected
    /// - Normalized notation for internal validation and consistent move representation
    /// </summary>
    public class ValidatedMove
    {
        /// <summary>
        /// The sequential number of the move in the game.
        /// </summary>
        public int MoveNumber { get; set; }

        /// <summary>
        /// The original notation of the move as detected from the input (image or text).
        /// This preserves the exact format as it was detected, which is useful for:
        /// - Debugging move detection issues
        /// - Showing users what was actually detected from their input
        /// - Maintaining the original format for reference
        /// </summary>
        public string Notation { get; set; }

        /// <summary>
        /// The standardized version of the move notation.
        /// This is particularly important for castling moves where different notations
        /// (e.g., "0-0", "O-O", "o-o") are normalized to a consistent format ("O-O").
        /// Used for:
        /// - Internal validation and move comparison
        /// - Generating consistent move suggestions
        /// - Displaying moves in a standardized format
        /// - Supporting move validation rules that require consistent notation
        /// </summary>
        public string NormalizedNotation { get; set; }

        /// <summary>
        /// The validation status of the move: "valid", "warning", or "error".
        /// </summary>
        public string ValidationStatus { get; set; }

        /// <summary>
        /// Detailed feedback about the validation result, including any warnings or errors.
        /// </summary>
        public string ValidationText { get; set; }
    }

    public class ChessMoveValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidatedMove> Moves { get; set; } = new();
    }

    public class ChessMoveValidator : IChessMoveValidator
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
                result.Moves.Add(new ValidatedMove
                {
                    MoveNumber = 0,
                    ValidationStatus = "error",
                    ValidationText = "No moves provided for validation"
                });
                return result;
            }

            for (int i = 0; i < moves.Length; i++)
            {
                var move = moves[i].Trim();
                var normalizedMove = NormalizeCastling(move);

                var moveValidation = ValidateSingleMove(normalizedMove, i + 1);

                result.Moves.Add(new ValidatedMove
                {
                    MoveNumber = i + 1,
                    Notation = move,
                    NormalizedNotation = normalizedMove,
                    ValidationStatus = moveValidation.IsValid ? "valid" :
                                     moveValidation.Moves.Any(m => m.ValidationStatus == "error") ? "error" : "warning",
                    ValidationText = string.Join("; ", moveValidation.Moves.Select(m => m.ValidationText))
                });

                if (!moveValidation.IsValid)
                {
                    result.IsValid = false;
                }
            }

            // Additional game-level validations
            ValidateGameLevelRules(result.Moves, result);

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
            var validatedMove = new ValidatedMove
            {
                MoveNumber = moveNumber,
                Notation = move,
                NormalizedNotation = move
            };

            // Check for empty or whitespace moves
            if (string.IsNullOrWhiteSpace(move))
            {
                result.IsValid = false;
                validatedMove.ValidationStatus = "error";
                validatedMove.ValidationText = "Empty or whitespace move";
                result.Moves.Add(validatedMove);
                return result;
            }

            // Basic syntax validation
            if (!_validMovePattern.IsMatch(move))
            {
                result.IsValid = false;
                validatedMove.ValidationStatus = "error";
                validatedMove.ValidationText = $"Invalid move syntax '{move}'";
                
                // Add promotion suggestion if applicable
                if (move.Contains("=") && !_validPromotions.Any(p => move.EndsWith(p)))
                {
                    validatedMove.ValidationText += "; Invalid promotion piece. Valid promotions are: =Q, =R, =B, =N";
                }
                
                result.Moves.Add(validatedMove);
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
                        validatedMove.ValidationStatus = "error";
                        validatedMove.ValidationText = $"Invalid piece notation '{piece}'";
                        result.Moves.Add(validatedMove);
                        return result;
                    }
                }
            }

            // If we get here, the move is valid
            validatedMove.ValidationStatus = "valid";
            validatedMove.ValidationText = "";
            result.Moves.Add(validatedMove);
            return result;
        }

        private void ValidateGameLevelRules(List<ValidatedMove> moves, ChessMoveValidationResult result)
        {
            // Check for consecutive checks
            for (int i = 0; i < moves.Count - 1; i++)
            {
                var currentMove = moves[i];
                var nextMove = moves[i + 1];

                if (currentMove.NormalizedNotation.EndsWith("+") && nextMove.NormalizedNotation.EndsWith("+"))
                {
                    // Update the validation status and text for both moves
                    currentMove.ValidationStatus = currentMove.ValidationStatus == "valid" ? "warning" : currentMove.ValidationStatus;
                    nextMove.ValidationStatus = nextMove.ValidationStatus == "valid" ? "warning" : nextMove.ValidationStatus;
                    
                    var warningText = "Consecutive checks detected. Please verify these moves.";
                    currentMove.ValidationText = string.IsNullOrEmpty(currentMove.ValidationText) ? 
                        warningText : currentMove.ValidationText + "; " + warningText;
                    nextMove.ValidationText = string.IsNullOrEmpty(nextMove.ValidationText) ? 
                        warningText : nextMove.ValidationText + "; " + warningText;
                }
            }
        }
    }
} 