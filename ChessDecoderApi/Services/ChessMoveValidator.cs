using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Chess;

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
        public string? Notation { get; set; }

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
        public string? NormalizedNotation { get; set; }

        /// <summary>
        /// The validation status of the move: "valid", "warning", or "error".
        /// </summary>
        public string? ValidationStatus { get; set; }

        /// <summary>
        /// Detailed feedback about the validation result, including any warnings or errors.
        /// </summary>
        public string? ValidationText { get; set; } = string.Empty;
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
        private static readonly Regex _castlingPattern = new(@"^[0OoΟοОо]-[0OoΟοОо](-[0OoΟοОо])?[+#]?$", RegexOptions.Compiled);


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
            validatedMove.ValidationText = string.Empty;
            result.Moves.Add(validatedMove);
            return result;
        }

        public void ValidateMovesInGameContext(ChessMoveValidationResult whiteValidation, ChessMoveValidationResult blackValidation)
        {
            try
            {
                var board = new ChessBoard();
                int maxMoves = Math.Max(whiteValidation.Moves.Count, blackValidation.Moves.Count);

                for (int i = 0; i < maxMoves; i++)
                {
                    // Validate white move
                    if (i < whiteValidation.Moves.Count)
                    {
                        var whiteMove = whiteValidation.Moves[i];
                        if (!string.IsNullOrWhiteSpace(whiteMove.NormalizedNotation))
                        {
                            var moveNotation = RemoveCheckAndCheckmate(whiteMove.NormalizedNotation);
                            // Store original validation status to check if move had invalid syntax
                            var hadInvalidSyntax = whiteMove.ValidationStatus == "error";
                            
                            try
                            {
                                // Move validates and executes the move, throws exception if invalid
                                board.Move(moveNotation);
                            }
                            catch (Exception ex)
                            {
                                // Check if board is in a terminal state (checkmate/stalemate) before attempting engine suggestions
                                var terminalState = GetTerminalState(board);
                                if (terminalState != null)
                                {
                                    // Board is in terminal state - no legal moves available
                                    whiteMove.ValidationStatus = "error";
                                    whiteMove.ValidationText = string.IsNullOrEmpty(whiteMove.ValidationText) 
                                        ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). {terminalState}" 
                                        : whiteMove.ValidationText + $"; Invalid move in game context: {ex.Message}. {terminalState}";
                                    whiteValidation.IsValid = false;
                                    continue; // Skip to next move since we can't proceed from terminal state
                                }

                                // Only try to replace moves that had invalid chess notation syntax
                                // Moves with valid syntax but invalid in context should not be replaced
                                if (hadInvalidSyntax)
                                {
                                    // Move had invalid syntax - try to get best move from engine
                                    string? bestMove = null;
                                    try
                                    {
                                        bestMove = GetBestMoveFromEngine(board, moveNotation);
                                    }
                                    catch (Exception engineEx)
                                    {
                                        _logger.LogWarning(engineEx, "Error getting best move from engine for white move '{Move}' at move {MoveNumber}", moveNotation, i + 1);
                                        // Continue to handle as if no move was found
                                    }

                                    if (!string.IsNullOrEmpty(bestMove))
                                    {
                                        try
                                        {
                                            // Replace invalid move with best move
                                            board.Move(bestMove);
                                            whiteMove.NormalizedNotation = bestMove;
                                            whiteMove.ValidationStatus = "warning";
                                            whiteMove.ValidationText = string.IsNullOrEmpty(whiteMove.ValidationText) 
                                                ? $"Invalid move '{moveNotation}' replaced with engine suggestion: '{bestMove}'" 
                                                : whiteMove.ValidationText + $"; Invalid move '{moveNotation}' replaced with engine suggestion: '{bestMove}'";
                                            _logger.LogInformation("Replaced invalid white move '{OriginalMove}' with engine suggestion '{BestMove}' at move {MoveNumber}", 
                                                moveNotation, bestMove, i + 1);
                                        }
                                        catch (Exception bestMoveEx)
                                        {
                                            // Even the best move failed, mark as error
                                            whiteMove.ValidationStatus = "error";
                                            whiteMove.ValidationText = string.IsNullOrEmpty(whiteMove.ValidationText) 
                                                ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). Engine suggestion '{bestMove}' also failed: {bestMoveEx.Message}" 
                                                : whiteMove.ValidationText + $"; Invalid move in game context: {ex.Message}. Engine suggestion failed: {bestMoveEx.Message}";
                                            whiteValidation.IsValid = false;
                                        }
                                    }
                                    else
                                    {
                                        // No legal moves available - check if it's due to terminal state
                                        var finalTerminalState = GetTerminalState(board);
                                        var errorMessage = finalTerminalState ?? "No legal moves available from this position.";
                                        
                                        whiteMove.ValidationStatus = "error";
                                        whiteMove.ValidationText = string.IsNullOrEmpty(whiteMove.ValidationText) 
                                            ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). {errorMessage}" 
                                            : whiteMove.ValidationText + $"; Invalid move in game context: {ex.Message}. {errorMessage}";
                                        whiteValidation.IsValid = false;
                                    }
                                }
                                else
                                {
                                    // Move has valid syntax but is invalid in game context - don't replace, just mark as error
                                    var finalTerminalState = GetTerminalState(board);
                                    var errorMessage = finalTerminalState ?? "No legal moves available from this position.";
                                    
                                    whiteMove.ValidationStatus = "error";
                                    whiteMove.ValidationText = string.IsNullOrEmpty(whiteMove.ValidationText) 
                                        ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). {errorMessage}" 
                                        : whiteMove.ValidationText + $"; Invalid move in game context: {ex.Message}. {errorMessage}";
                                    whiteValidation.IsValid = false;
                                }
                            }
                        }
                    }

                    // Validate black move
                    if (i < blackValidation.Moves.Count)
                    {
                        var blackMove = blackValidation.Moves[i];
                        if (!string.IsNullOrWhiteSpace(blackMove.NormalizedNotation))
                        {
                            var moveNotation = RemoveCheckAndCheckmate(blackMove.NormalizedNotation);
                            // Store original validation status to check if move had invalid syntax
                            var hadInvalidSyntax = blackMove.ValidationStatus == "error";
                            
                            try
                            {
                                // Move validates and executes the move, throws exception if invalid
                                board.Move(moveNotation);
                            }
                            catch (Exception ex)
                            {
                                // Check if board is in a terminal state (checkmate/stalemate) before attempting engine suggestions
                                var terminalState = GetTerminalState(board);
                                if (terminalState != null)
                                {
                                    // Board is in terminal state - no legal moves available
                                    blackMove.ValidationStatus = "error";
                                    blackMove.ValidationText = string.IsNullOrEmpty(blackMove.ValidationText) 
                                        ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). {terminalState}" 
                                        : blackMove.ValidationText + $"; Invalid move in game context: {ex.Message}. {terminalState}";
                                    blackValidation.IsValid = false;
                                    continue; // Skip to next move since we can't proceed from terminal state
                                }

                                // Only try to replace moves that had invalid chess notation syntax
                                // Moves with valid syntax but invalid in context should not be replaced
                                if (hadInvalidSyntax)
                                {
                                    // Move had invalid syntax - try to get best move from engine
                                    string? bestMove = null;
                                    try
                                    {
                                        bestMove = GetBestMoveFromEngine(board, moveNotation);
                                    }
                                    catch (Exception engineEx)
                                    {
                                        _logger.LogWarning(engineEx, "Error getting best move from engine for black move '{Move}' at move {MoveNumber}", moveNotation, i + 1);
                                        // Continue to handle as if no move was found
                                    }

                                    if (!string.IsNullOrEmpty(bestMove))
                                    {
                                        try
                                        {
                                            // Replace invalid move with best move
                                            board.Move(bestMove);
                                            blackMove.NormalizedNotation = bestMove;
                                            blackMove.ValidationStatus = "warning";
                                            blackMove.ValidationText = string.IsNullOrEmpty(blackMove.ValidationText) 
                                                ? $"Invalid move '{moveNotation}' replaced with engine suggestion: '{bestMove}'" 
                                                : blackMove.ValidationText + $"; Invalid move '{moveNotation}' replaced with engine suggestion: '{bestMove}'";
                                            _logger.LogInformation("Replaced invalid black move '{OriginalMove}' with engine suggestion '{BestMove}' at move {MoveNumber}", 
                                                moveNotation, bestMove, i + 1);
                                        }
                                        catch (Exception bestMoveEx)
                                        {
                                            // Even the best move failed, mark as error
                                            blackMove.ValidationStatus = "error";
                                            blackMove.ValidationText = string.IsNullOrEmpty(blackMove.ValidationText) 
                                                ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). Engine suggestion '{bestMove}' also failed: {bestMoveEx.Message}" 
                                                : blackMove.ValidationText + $"; Invalid move in game context: {ex.Message}. Engine suggestion failed: {bestMoveEx.Message}";
                                            blackValidation.IsValid = false;
                                        }
                                    }
                                    else
                                    {
                                        // No legal moves available - check if it's due to terminal state
                                        var finalTerminalState = GetTerminalState(board);
                                        var errorMessage = finalTerminalState ?? "No legal moves available from this position.";
                                        
                                        blackMove.ValidationStatus = "error";
                                        blackMove.ValidationText = string.IsNullOrEmpty(blackMove.ValidationText) 
                                            ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). {errorMessage}" 
                                            : blackMove.ValidationText + $"; Invalid move in game context: {ex.Message}. {errorMessage}";
                                        blackValidation.IsValid = false;
                                    }
                                }
                                else
                                {
                                    // Move has valid syntax but is invalid in game context - don't replace, just mark as error
                                    var finalTerminalState = GetTerminalState(board);
                                    var errorMessage = finalTerminalState ?? "No legal moves available from this position.";
                                    
                                    blackMove.ValidationStatus = "error";
                                    blackMove.ValidationText = string.IsNullOrEmpty(blackMove.ValidationText) 
                                        ? $"Invalid move in game context: '{moveNotation}' is not a legal move ({ex.Message}). {errorMessage}" 
                                        : blackMove.ValidationText + $"; Invalid move in game context: {ex.Message}. {errorMessage}";
                                    blackValidation.IsValid = false;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating moves in game context");
                // Mark all moves as having warning if we can't validate
                foreach (var move in whiteValidation.Moves)
                {
                    if (move.ValidationStatus == "valid")
                    {
                        move.ValidationStatus = "warning";
                        move.ValidationText = string.IsNullOrEmpty(move.ValidationText) 
                            ? "Unable to validate move in game context" 
                            : move.ValidationText + "; Unable to validate move in game context";
                    }
                }
                foreach (var move in blackValidation.Moves)
                {
                    if (move.ValidationStatus == "valid")
                    {
                        move.ValidationStatus = "warning";
                        move.ValidationText = string.IsNullOrEmpty(move.ValidationText) 
                            ? "Unable to validate move in game context" 
                            : move.ValidationText + "; Unable to validate move in game context";
                    }
                }
            }
        }

        /// <summary>
        /// Gets the best move from the chess engine for the current board position.
        /// First prioritizes moves similar to the original invalid move (using Levenshtein distance),
        /// then uses move evaluation for ties.
        /// </summary>
        /// <param name="board">The current chess board position</param>
        /// <param name="originalMove">The original invalid move to compare against</param>
        /// <returns>The best move in SAN notation, or null if no legal moves are available</returns>
        private string? GetBestMoveFromEngine(ChessBoard board, string? originalMove = null)
        {
            try
            {
                // Get all legal moves from the current position
                var legalMoves = GetLegalMoves(board);

                _logger.LogInformation("Legal moves: {LegalMoves}", string.Join(", ", legalMoves));
                if (legalMoves == null || legalMoves.Count == 0)
                {
                    _logger.LogWarning("No legal moves available from current position");
                    return null;
                }

                // Normalize original move for comparison (remove non-important characters)
                var normalizedOriginal = NormalizeMoveForComparison(originalMove ?? string.Empty);

                // Evaluate all moves and calculate distance from original
                var moveCandidates = new List<(string Move, int Distance, int Score)>();

                foreach (var move in legalMoves)
                {
                    try
                    {
                        // Calculate Levenshtein distance from original move
                        var normalizedMove = NormalizeMoveForComparison(move);
                        var distance = CalculateLevenshteinDistance(normalizedOriginal, normalizedMove);

                        // Strong preference: if the destination square matches, reduce distance significantly
                        // This handles cases like "Bc3" -> "c3" where the square matches but piece indicator differs
                        if (normalizedOriginal.Length >= 2 && normalizedMove.Length >= 2)
                        {
                            var originalSquare = normalizedOriginal.Substring(normalizedOriginal.Length - 2);
                            var moveSquare = normalizedMove.Substring(normalizedMove.Length - 2);
                            
                            if (originalSquare == moveSquare)
                            {
                                // Destination square matches - this is very similar (likely OCR/detection error on piece)
                                distance = Math.Max(0, distance - 3); // Strong preference for matching squares
                                _logger.LogDebug("Move '{Move}' matches destination square '{Square}' with original, reducing distance", move, moveSquare);
                            }
                        }

                        // Test the move on a copy of the board for evaluation
                        var testBoard = CloneBoard(board);
                        int moveScore;
                        if (testBoard == null)
                        {
                            // If cloning fails, just evaluate based on move notation
                            moveScore = EvaluateMoveByNotation(move);
                        }
                        else
                        {
                            testBoard.Move(move);
                            moveScore = EvaluateMove(move, testBoard);
                        }

                        moveCandidates.Add((move, distance, moveScore));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error evaluating move '{Move}'", move);
                        // Skip this move and continue
                        continue;
                    }
                }

                if (moveCandidates.Count == 0)
                {
                    return null;
                }

                // First, find the minimum distance (most similar to original)
                var minDistance = moveCandidates.Min(c => c.Distance);
                
                // Filter to moves with minimum distance
                var closestMoves = moveCandidates.Where(c => c.Distance == minDistance).ToList();

                // If there's a tie, pick the one with the best evaluation score
                var bestCandidate = closestMoves.OrderByDescending(c => c.Score).First();

                _logger.LogInformation("Selected move '{BestMove}' (distance: {Distance}, score: {Score}) from {TotalMoves} legal moves", 
                    bestCandidate.Move, bestCandidate.Distance, bestCandidate.Score, legalMoves.Count);

                return bestCandidate.Move;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting best move from engine");
                return null;
            }
        }

        /// <summary>
        /// Clones a ChessBoard by creating a new board and replaying all moves.
        /// This is a fallback method when direct cloning is not available.
        /// </summary>
        private ChessBoard? CloneBoard(ChessBoard board)
        {
            try
            {
                // Try to get FEN using reflection
                var boardType = board.GetType();
                var fenProperty = boardType.GetProperty("Fen") ?? 
                                 boardType.GetProperty("FEN") ??
                                 boardType.GetProperty("Position");
                
                if (fenProperty != null)
                {
                    var fen = fenProperty.GetValue(board)?.ToString();
                    if (!string.IsNullOrEmpty(fen))
                    {
                        // Try to create a new board with FEN
                        var constructor = typeof(ChessBoard).GetConstructor(new[] { typeof(string) });
                        if (constructor != null)
                        {
                            return (ChessBoard)constructor.Invoke(new[] { fen });
                        }
                    }
                }

                // If FEN approach doesn't work, return null to use fallback evaluation
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets all legal moves from the current board position.
        /// Uses the Moves() method from ChessBoard to get legal moves.
        /// </summary>
        /// <param name="board">The current chess board position</param>
        /// <returns>List of legal moves in SAN notation</returns>
        private List<string> GetLegalMoves(ChessBoard board)
        {
            var legalMoves = new List<string>();

            try
            {
                // Use reflection to call the Moves() method
                var boardType = board.GetType();
                var movesMethod = boardType.GetMethod("Moves", new[] { typeof(bool), typeof(bool) });
                
                if (movesMethod != null)
                {
                    // Call Moves(allowAmbiguousCastle: false, generateSan: true)
                    var moves = movesMethod.Invoke(board, new object[] { false, true });
                    
                    if (moves is IEnumerable<object> moveList)
                    {
                        foreach (var move in moveList)
                        {
                            // Try to get SAN notation from the move object
                            var moveType = move.GetType();
                            
                            // Try common property names for SAN notation
                            var sanProperty = moveType.GetProperty("San") ?? 
                                            moveType.GetProperty("Notation") ?? 
                                            moveType.GetProperty("ToString");
                            
                            if (sanProperty != null)
                            {
                                var san = sanProperty.GetValue(move)?.ToString();
                                if (!string.IsNullOrEmpty(san))
                                {
                                    legalMoves.Add(san);
                                }
                            }
                            else
                            {
                                // Fallback to ToString
                                var moveStr = move.ToString();
                                if (!string.IsNullOrEmpty(moveStr))
                                {
                                    legalMoves.Add(moveStr);
                                }
                            }
                        }
                    }
                }
                
                // If still no moves found, try the testing method as fallback
                if (legalMoves.Count == 0)
                {
                    _logger.LogWarning("Moves() method returned no moves, falling back to testing method");
                    legalMoves = GetLegalMovesByTesting(board);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting legal moves using Moves() method, falling back to testing method");
                legalMoves = GetLegalMovesByTesting(board);
            }

            return legalMoves;
        }

        /// <summary>
        /// Gets legal moves by testing all possible move patterns.
        /// This is a fallback method when reflection doesn't work.
        /// Note: This is a simplified approach that may not find all legal moves.
        /// </summary>
        private List<string> GetLegalMovesByTesting(ChessBoard board)
        {
            var legalMoves = new List<string>();
            var files = new[] { "a", "b", "c", "d", "e", "f", "g", "h" };
            var ranks = new[] { "1", "2", "3", "4", "5", "6", "7", "8" };
            var pieces = new[] { "K", "Q", "R", "B", "N" };

            // Test castling moves
            try
            {
                var testBoard = CloneBoard(board);
                if (testBoard != null)
                {
                    testBoard.Move("O-O");
                    legalMoves.Add("O-O");
                }
            }
            catch { }

            try
            {
                var testBoard = CloneBoard(board);
                if (testBoard != null)
                {
                    testBoard.Move("O-O-O");
                    legalMoves.Add("O-O-O");
                }
            }
            catch { }

            // Test a limited set of common moves (to avoid performance issues)
            // This is a simplified approach - in production, you'd want a more comprehensive method
            foreach (var file in files)
            {
                foreach (var rank in ranks)
                {
                    var square = file + rank;
                    
                    // Test pawn moves
                    try
                    {
                        var testBoard = CloneBoard(board);
                        if (testBoard != null)
                        {
                            testBoard.Move(square);
                            legalMoves.Add(square);
                        }
                    }
                    catch { }

                    // Test piece moves (limited to avoid too many tests)
                    foreach (var piece in pieces)
                    {
                        try
                        {
                            var testBoard = CloneBoard(board);
                            if (testBoard != null)
                            {
                                var move = piece + square;
                                testBoard.Move(move);
                                legalMoves.Add(move);
                            }
                        }
                        catch { }

                        // Test captures
                        try
                        {
                            var testBoard = CloneBoard(board);
                            if (testBoard != null)
                            {
                                var move = piece + "x" + square;
                                testBoard.Move(move);
                                legalMoves.Add(move);
                            }
                        }
                        catch { }
                    }
                }
            }

            return legalMoves;
        }

        /// <summary>
        /// Evaluates a move and returns a score. Higher scores are better.
        /// Scoring: captures > checks > other moves
        /// </summary>
        private int EvaluateMove(string move, ChessBoard board)
        {
            // Start with base evaluation from notation
            int score = EvaluateMoveByNotation(move);

            // Additional evaluation based on board state if possible
            try
            {
                // Try to check if the move gives check using reflection
                var boardType = board.GetType();
                var isCheckProperty = boardType.GetProperty("IsCheck") ?? 
                                     boardType.GetProperty("InCheck");
                if (isCheckProperty != null)
                {
                    var isCheck = (bool)(isCheckProperty.GetValue(board) ?? false);
                    if (isCheck)
                    {
                        score += 50; // Bonus for moves that give check
                    }
                }
            }
            catch { }

            return score;
        }

        /// <summary>
        /// Evaluates a move based on its notation only (without board state).
        /// This is used as a fallback when board evaluation is not possible.
        /// </summary>
        private int EvaluateMoveByNotation(string move)
        {
            int score = 0;

            // Prefer captures (moves with 'x')
            if (move.Contains('x'))
            {
                score += 100;
            }

            // Prefer center squares (e4, e5, d4, d5)
            if (move.Contains("e4") || move.Contains("e5") || move.Contains("d4") || move.Contains("d5"))
            {
                score += 10;
            }

            // Prefer developing pieces (moving knights and bishops)
            if (move.StartsWith("N") || move.StartsWith("B"))
            {
                score += 5;
            }

            // Prefer castling (good for king safety)
            if (move.StartsWith("O-O"))
            {
                score += 15;
            }

            return score;
        }

        /// <summary>
        /// Normalizes a move string for comparison by removing non-important characters.
        /// Removes: '+', 'x', '#', '=', and whitespace for distance calculation.
        /// </summary>
        private string NormalizeMoveForComparison(string move)
        {
            if (string.IsNullOrEmpty(move))
                return string.Empty;

            // Remove non-important characters: check (+), checkmate (#), capture (x), promotion (=)
            var normalized = move.Replace("+", "")
                                 .Replace("#", "")
                                 .Replace("x", "")
                                 .Replace("=", "")
                                 .Replace(" ", "")
                                 .ToUpperInvariant();

            return normalized;
        }

        /// <summary>
        /// Calculates the Levenshtein distance between two strings.
        /// Returns the minimum number of single-character edits needed to transform one string into another.
        /// </summary>
        private int CalculateLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
                return string.IsNullOrEmpty(t) ? 0 : t.Length;

            if (string.IsNullOrEmpty(t))
                return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // Initialize first row and column
            for (int i = 0; i <= n; i++)
                d[i, 0] = i;

            for (int j = 0; j <= m; j++)
                d[0, j] = j;

            // Fill the matrix
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1,      // deletion
                                 d[i, j - 1] + 1),      // insertion
                        d[i - 1, j - 1] + cost);        // substitution
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// Removes check (+) and checkmate (#) symbols from move notation for validation
        /// </summary>
        private string RemoveCheckAndCheckmate(string move)
        {
            if (string.IsNullOrEmpty(move))
                return move;
            
            return move.TrimEnd('+', '#');
        }

        /// <summary>
        /// Checks if the board is in a terminal state (checkmate or stalemate).
        /// Returns a descriptive message if terminal, null otherwise.
        /// </summary>
        private string? GetTerminalState(ChessBoard board)
        {
            try
            {
                // First check if there are any legal moves available
                var legalMoves = GetLegalMoves(board);
                if (legalMoves == null || legalMoves.Count == 0)
                {
                    // No legal moves - could be checkmate or stalemate
                    // Try to determine which by checking if the current player is in check
                    var boardType = board.GetType();
                    var isCheckProperty = boardType.GetProperty("IsCheck") ?? 
                                         boardType.GetProperty("InCheck");
                    
                    bool isInCheck = false;
                    if (isCheckProperty != null)
                    {
                        try
                        {
                            isInCheck = (bool)(isCheckProperty.GetValue(board) ?? false);
                        }
                        catch
                        {
                            // If we can't determine check state, assume stalemate
                        }
                    }

                    if (isInCheck)
                    {
                        return "Game is in checkmate - no legal moves available.";
                    }
                    else
                    {
                        return "Game is in stalemate - no legal moves available.";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking terminal state of board");
                // If we can't determine the state, return null to allow normal error handling
            }

            return null; // Not in terminal state
        }

        private void ValidateGameLevelRules(List<ValidatedMove> moves, ChessMoveValidationResult result)
        {
            // Check for consecutive checks
            for (int i = 0; i < moves.Count - 1; i++)
            {
                var currentMove = moves[i];
                var nextMove = moves[i + 1];

                if (!string.IsNullOrEmpty(currentMove.NormalizedNotation) && currentMove.NormalizedNotation.EndsWith("+") &&
                    !string.IsNullOrEmpty(nextMove.NormalizedNotation) && nextMove.NormalizedNotation.EndsWith("+"))
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