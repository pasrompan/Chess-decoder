namespace ChessDecoderApi.Services
{
    public interface IChessMoveValidator
    {
        ChessMoveValidationResult ValidateMoves(string[] moves);
        
        /// <summary>
        /// Validates moves in game context by alternating white and black moves.
        /// Updates the validation status of moves to error or warning if they are invalid in the game context.
        /// </summary>
        /// <param name="whiteValidation">Validation result for white moves</param>
        /// <param name="blackValidation">Validation result for black moves</param>
        void ValidateMovesInGameContext(ChessMoveValidationResult whiteValidation, ChessMoveValidationResult blackValidation);
    }
} 