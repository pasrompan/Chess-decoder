using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chess;
using ChessDecoderApi.Services;
using Microsoft.Extensions.Logging;

namespace ChessDecoderApi.Tests.DiagnosticTests
{
    /// <summary>
    /// Diagnostic program to understand why GetLegalMoves isn't finding moves.
    /// Run this as a console app or unit test to see what's available on ChessBoard.
    /// </summary>
    public class GetLegalMovesDiagnostic
    {
        public static void RunDiagnostics()
        {
            Console.WriteLine("=== GetLegalMoves Diagnostic ===");
            Console.WriteLine();

            var board = new ChessBoard();
            var boardType = board.GetType();

            // 1. Check available properties
            Console.WriteLine("1. Available Properties on ChessBoard:");
            var properties = boardType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var prop in properties.OrderBy(p => p.Name))
            {
                try
                {
                    var value = prop.GetValue(board);
                    Console.WriteLine($"   - {prop.Name} ({prop.PropertyType.Name}): {value}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   - {prop.Name} ({prop.PropertyType.Name}): [Error accessing: {ex.Message}]");
                }
            }
            Console.WriteLine();

            // 2. Check available methods
            Console.WriteLine("2. Available Methods on ChessBoard (filtered for 'Move'):");
            var methods = boardType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var method in methods.Where(m => m.Name.Contains("Move", StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.Name))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"   - {method.Name}({parameters})");
            }
            Console.WriteLine();

            // 3. Try to find LegalMoves property
            Console.WriteLine("3. Searching for LegalMoves property:");
            var legalMovesProperty = boardType.GetProperty("LegalMoves", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (legalMovesProperty != null)
            {
                Console.WriteLine($"   ✓ Found LegalMoves property: {legalMovesProperty.Name}");
                try
                {
                    var moves = legalMovesProperty.GetValue(board);
                    Console.WriteLine($"   Type: {moves?.GetType().Name}");
                    if (moves is IEnumerable<object> moveList)
                    {
                        var count = moveList.Count();
                        Console.WriteLine($"   Count: {count}");
                        if (count > 0)
                        {
                            Console.WriteLine($"   First 5 moves:");
                            foreach (var move in moveList.Take(5))
                            {
                                Console.WriteLine($"     - {move} ({move.GetType().Name})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error accessing LegalMoves: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("   ✗ LegalMoves property not found");
            }
            Console.WriteLine();

            // 4. Try to find GetLegalMoves method
            Console.WriteLine("4. Searching for GetLegalMoves method:");
            var getLegalMovesMethod = boardType.GetMethod("GetLegalMoves", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (getLegalMovesMethod != null)
            {
                Console.WriteLine($"   ✓ Found GetLegalMoves method");
                try
                {
                    var moves = getLegalMovesMethod.Invoke(board, null);
                    Console.WriteLine($"   Return type: {moves?.GetType().Name}");
                    if (moves is IEnumerable<object> moveList)
                    {
                        var count = moveList.Count();
                        Console.WriteLine($"   Count: {count}");
                        if (count > 0)
                        {
                            Console.WriteLine($"   First 5 moves:");
                            foreach (var move in moveList.Take(5))
                            {
                                Console.WriteLine($"     - {move} ({move.GetType().Name})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error calling GetLegalMoves: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("   ✗ GetLegalMoves method not found");
            }
            Console.WriteLine();

            // 5. Try to find FEN property
            Console.WriteLine("5. Searching for FEN property:");
            var fenProperty = boardType.GetProperty("Fen", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic) ??
                            boardType.GetProperty("FEN", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic) ??
                            boardType.GetProperty("Position", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (fenProperty != null)
            {
                Console.WriteLine($"   ✓ Found FEN property: {fenProperty.Name}");
                try
                {
                    var fen = fenProperty.GetValue(board)?.ToString();
                    Console.WriteLine($"   FEN value: {fen}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error accessing FEN: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("   ✗ FEN property not found");
            }
            Console.WriteLine();

            // 6. Test actual GetLegalMoves from ChessMoveValidator
            Console.WriteLine("6. Testing ChessMoveValidator.GetLegalMoves:");
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<ChessMoveValidator>();
            var validator = new ChessMoveValidator(logger);
            
            var validatorGetLegalMovesMethod = typeof(ChessMoveValidator).GetMethod("GetLegalMoves", BindingFlags.NonPublic | BindingFlags.Instance);
            if (validatorGetLegalMovesMethod != null)
            {
                try
                {
                    var legalMoves = (List<string>)validatorGetLegalMovesMethod.Invoke(validator, new object[] { board })!;
                    Console.WriteLine($"   Result count: {legalMoves.Count}");
                    if (legalMoves.Count > 0)
                    {
                        Console.WriteLine($"   First 10 moves:");
                        foreach (var move in legalMoves.Take(10))
                        {
                            Console.WriteLine($"     - {move}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ✗ No legal moves found!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error calling GetLegalMoves: {ex.Message}");
                    Console.WriteLine($"   Stack trace: {ex.StackTrace}");
                }
            }
            else
            {
                Console.WriteLine("   ✗ GetLegalMoves method not found in ChessMoveValidator");
            }
            Console.WriteLine();

            // 7. Test CloneBoard
            Console.WriteLine("7. Testing CloneBoard:");
            var cloneBoardMethod = typeof(ChessMoveValidator).GetMethod("CloneBoard", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cloneBoardMethod != null)
            {
                try
                {
                    board.Move("e4");
                    var clonedBoard = (ChessBoard?)cloneBoardMethod.Invoke(validator, new object[] { board });
                    if (clonedBoard != null)
                    {
                        Console.WriteLine("   ✓ CloneBoard succeeded");
                        try
                        {
                            clonedBoard.Move("e5");
                            Console.WriteLine("   ✓ Cloned board is functional");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"   ✗ Cloned board is not functional: {ex.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ✗ CloneBoard returned null");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error calling CloneBoard: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("   ✗ CloneBoard method not found");
            }
            Console.WriteLine();

            // 8. Test with FEN string
            Console.WriteLine("8. Testing with FEN string:");
            const string testFen = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 0 1";
            Console.WriteLine($"   FEN: {testFen}");
            
            // Try to find constructor or method that accepts FEN
            var constructors = boardType.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            Console.WriteLine("   Available constructors:");
            foreach (var constructor in constructors)
            {
                var parameters = string.Join(", ", constructor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"     - ChessBoard({parameters})");
            }
            
            // Try to find methods that might load from FEN
            var allMethods = boardType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            var fenMethods = allMethods.Where(m => 
                m.Name.Contains("Fen", StringComparison.OrdinalIgnoreCase) || 
                m.Name.Contains("FEN", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("Load", StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains("From", StringComparison.OrdinalIgnoreCase)).ToList();
            
            Console.WriteLine("   Methods that might work with FEN:");
            if (fenMethods.Any())
            {
                foreach (var method in fenMethods)
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"     - {method.Name}({parameters})");
                }
            }
            else
            {
                Console.WriteLine("     (No FEN-related methods found)");
            }
            
            // Try to create board from FEN using constructor
            ChessBoard? fenBoard = null;
            try
            {
                var fenConstructor = boardType.GetConstructor(new[] { typeof(string) });
                if (fenConstructor != null)
                {
                    fenBoard = (ChessBoard)fenConstructor.Invoke(new object[] { testFen });
                    Console.WriteLine("   ✓ Created board from FEN using constructor");
                }
                else
                {
                    Console.WriteLine("   ✗ No constructor found that accepts string (FEN)");
                    
                    // Try static methods that might create from FEN
                    var staticMethods = boardType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                    var fenStaticMethods = staticMethods.Where(m => 
                        m.Name.Contains("Fen", StringComparison.OrdinalIgnoreCase) || 
                        m.Name.Contains("From", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("Parse", StringComparison.OrdinalIgnoreCase) ||
                        m.Name.Contains("Load", StringComparison.OrdinalIgnoreCase)).ToList();
                    
                    if (fenStaticMethods.Any())
                    {
                        Console.WriteLine("   Trying static methods that might work with FEN:");
                        foreach (var method in fenStaticMethods)
                        {
                            var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            Console.WriteLine($"     - {method.Name}({parameters})");
                            
                            // Try methods that accept string
                            if (method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string))
                            {
                                try
                                {
                                    fenBoard = (ChessBoard?)method.Invoke(null, new object[] { testFen });
                                    if (fenBoard != null)
                                    {
                                        Console.WriteLine($"     ✓ Successfully created board using {method.Name}()");
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"     ✗ {method.Name}() failed: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    // Try LoadFromFen static method
                    if (fenBoard == null)
                    {
                        var loadFromFenMethod = boardType.GetMethod("LoadFromFen", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(AutoEndgameRules) }, null);
                        if (loadFromFenMethod != null)
                        {
                            try
                            {
                                // Get AutoEndgameRules enum value (default is usually None)
                                var autoEndgameRulesType = typeof(AutoEndgameRules);
                                var noneValue = Enum.GetValues(autoEndgameRulesType).Cast<AutoEndgameRules>().FirstOrDefault();
                                fenBoard = (ChessBoard?)loadFromFenMethod.Invoke(null, new object[] { testFen, noneValue });
                                if (fenBoard != null)
                                {
                                    Console.WriteLine("   ✓ Created board from FEN using LoadFromFen()");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"   ✗ LoadFromFen() failed: {ex.Message}");
                            }
                        }
                        
                        // If LoadFromFen failed, try TryLoadFromFen
                        if (fenBoard == null)
                        {
                            var tryLoadFromFenMethod = boardType.GetMethod("TryLoadFromFen", BindingFlags.Public | BindingFlags.Static);
                            if (tryLoadFromFenMethod != null)
                            {
                                try
                                {
                                    var parameters = tryLoadFromFenMethod.GetParameters();
                                    var args = new object[parameters.Length];
                                    args[0] = testFen;
                                    args[1] = Activator.CreateInstance(parameters[1].ParameterType.GetElementType()!)!; // out parameter
                                    if (parameters.Length > 2)
                                    {
                                        var autoEndgameRulesType = typeof(AutoEndgameRules);
                                        var noneValue = Enum.GetValues(autoEndgameRulesType).Cast<AutoEndgameRules>().FirstOrDefault();
                                        args[2] = noneValue;
                                    }
                                    
                                    var result = (bool)tryLoadFromFenMethod.Invoke(null, args)!;
                                    if (result)
                                    {
                                        fenBoard = (ChessBoard)args[1];
                                        Console.WriteLine("   ✓ Created board from FEN using TryLoadFromFen()");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"   ✗ TryLoadFromFen() failed: {ex.Message}");
                                }
                            }
                        }
                        
                        // If still no board, try to manually build the position by making moves
                        if (fenBoard == null)
                        {
                            Console.WriteLine("   Attempting to build position manually from moves:");
                            Console.WriteLine("   (This FEN represents: 1.e4 e5 2.Nf3 Nc6)");
                            try
                            {
                                fenBoard = new ChessBoard();
                                fenBoard.Move("e4");
                                fenBoard.Move("e5");
                                fenBoard.Move("Nf3");
                                fenBoard.Move("Nc6");
                                Console.WriteLine("   ✓ Created board by replaying moves");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"   ✗ Error building position manually: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ✗ Error creating board from FEN: {ex.Message}");
            }
            
            // If we got a board from FEN, test GetLegalMoves on it
            if (fenBoard != null)
            {
                Console.WriteLine();
                Console.WriteLine("9. Testing GetLegalMoves with FEN board:");
                try
                {
                    var legalMovesFromFen = (List<string>)validatorGetLegalMovesMethod.Invoke(validator, new object[] { fenBoard })!;
                    Console.WriteLine($"   Result count: {legalMovesFromFen.Count}");
                    if (legalMovesFromFen.Count > 0)
                    {
                        Console.WriteLine($"   All legal moves from FEN position:");
                        foreach (var move in legalMovesFromFen)
                        {
                            Console.WriteLine($"     - {move}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("   ✗ No legal moves found from FEN position!");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error getting legal moves from FEN board: {ex.Message}");
                }
            }
        }
    }
}

