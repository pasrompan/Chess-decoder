namespace ChessDecoderApi.Services
{
    public interface IChessMoveValidator
    {
        ChessMoveValidationResult ValidateMoves(string[] moves);
    }
} 