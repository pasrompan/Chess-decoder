using System.Threading.Tasks;

namespace ChessDecoderApi.Services
{
    public interface IChessMoveProcessor
    {
        Task<string[]> ProcessChessMovesAsync(string rawText);
    }
} 