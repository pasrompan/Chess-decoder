using System.Collections.Generic;

namespace ChessDecoderApi.Models
{
    public class ChessGameResponse
    {
        public string PgnContent { get; set; }
        public ChessGameValidation Validation { get; set; }
    }

    public class ChessGameValidation
    {
        public string GameId { get; set; }
        public List<ChessMovePair> Moves { get; set; } = new();
    }

    public class ChessMovePair
    {
        public int MoveNumber { get; set; }
        public ValidatedMove WhiteMove { get; set; }
        public ValidatedMove BlackMove { get; set; }
    }

    public class ValidatedMove
    {
        public string Notation { get; set; }
        public string NormalizedNotation { get; set; }
        public string ValidationStatus { get; set; }
        public string ValidationText { get; set; }
    }
} 