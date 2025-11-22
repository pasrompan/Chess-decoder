using System;
using ChessDecoderApi.Tests.DiagnosticTests;
using Xunit;

namespace ChessDecoderApi.Tests.DiagnosticTests
{
    public class GetLegalMovesDiagnosticTest
    {
        [Fact]
        public void RunDiagnostics()
        {
            // This test will output diagnostic information to help understand
            // why GetLegalMoves isn't finding any moves
            GetLegalMovesDiagnostic.RunDiagnostics();
        }
    }
}

