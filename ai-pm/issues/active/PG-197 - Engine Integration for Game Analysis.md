---
id: PG-197
status: active
priority_score: .6250
effort: 8
impact: 5
dependencies: []
created_date: "2025-12-31"
updated_date: "2025-12-31"
plan_type: agent_plan
executable: false
---

# Implementation Plan: Engine Integration for Game Analysis

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Integrate a chess engine (e.g., Stockfish) to analyze processed chess games and provide users with:
- Move-by-move evaluations (position evaluation scores)
- Blunder detection (moves that significantly worsen the position)
- Best move suggestions (what the engine recommends instead of played moves)
- Game analysis summary (overall game quality, critical moments)

This feature will enhance user engagement by providing educational value and helping users understand their games better.

## Plan Overview

The system should:
1. Integrate a chess engine (Stockfish) into the backend service layer
2. Analyze games move-by-move using the stored PGN content
3. Store analysis results (evaluations, blunders, best moves) in the database
4. Provide API endpoints to trigger analysis and retrieve results
5. Display analysis results in the frontend with visual indicators and interactive components
6. Support both on-demand analysis and automatic analysis after game processing

## Implementation Plan

### Phase 1: Chess Engine Integration Setup

**Agent should:**
- Research and select chess engine library for .NET (options: Stockfish.NET, Chess.NET with UCI engine integration, or direct UCI protocol implementation)
- Evaluate options:
  - Stockfish.NET wrapper (if available)
  - Direct UCI protocol communication with Stockfish executable
  - Chess.NET library with engine integration
- Set up engine dependency management:
  - Include Stockfish executable or package in deployment
  - Configure engine path and settings
  - Handle engine initialization and lifecycle
- Create engine wrapper interface `IChessEngine` for abstraction
- Implement engine communication layer (UCI protocol handling)
- Add configuration settings for engine depth, time limits, and analysis parameters
- Handle engine process management (start, stop, restart on errors)

**Key Integration Points:**
- Configuration system (`appsettings.json`)
- Dependency injection container
- Logging infrastructure

**Deliverables:**
- `IChessEngine` interface definition
- `StockfishEngine` implementation (or selected engine wrapper)
- Engine configuration settings
- Engine process management utilities

### Phase 2: Analysis Service Implementation

**Agent should:**
- Create `IGameAnalysisService` interface for game analysis operations
- Implement `GameAnalysisService` with methods:
  - `AnalyzeGameAsync(Guid gameId, AnalysisOptions options)`: Analyze entire game
  - `AnalyzePositionAsync(string fen, int depth)`: Analyze specific position
  - `GetBestMoveAsync(string fen, int depth)`: Get best move for position
  - `EvaluatePositionAsync(string fen)`: Get position evaluation score
- Implement move-by-move analysis:
  - Parse PGN content to extract moves
  - Replay game move by move
  - Evaluate each position after each move
  - Compare played move with engine's best move
  - Calculate evaluation delta (how much the position changed)
- Implement blunder detection logic:
  - Define blunder thresholds (e.g., evaluation drop > 200 centipawns)
  - Identify critical mistakes, blunders, and inaccuracies
  - Categorize move quality (excellent, good, inaccuracy, mistake, blunder)
- Implement analysis result aggregation:
  - Calculate game statistics (average evaluation, blunder count, etc.)
  - Identify critical moments (biggest blunders, best moves)
  - Generate analysis summary

**Key Integration Points:**
- `ChessGame` model and repository for retrieving PGN content
- PGN parsing utilities (may need to add or use existing library)
- Chess position/FEN handling
- Error handling for invalid PGN or engine failures

**Deliverables:**
- `IGameAnalysisService` interface
- `GameAnalysisService` implementation
- Analysis result models and DTOs
- Blunder detection algorithms

### Phase 3: Data Model Extensions

**Agent should:**
- Create `GameAnalysis` model to store analysis results:
  - `Id` (Guid, primary key)
  - `ChessGameId` (Guid, foreign key to ChessGame)
  - `AnalyzedAt` (DateTime)
  - `EngineVersion` (string, e.g., "Stockfish 16")
  - `AnalysisDepth` (int, depth used for analysis)
  - `AnalysisTimeMs` (int, time taken for analysis)
  - `MoveAnalyses` (collection of MoveAnalysis entries)
  - `GameSummary` (aggregated statistics)
- Create `MoveAnalysis` model:
  - `MoveNumber` (int, move number in game)
  - `Move` (string, move notation)
  - `FenBefore` (string, position before move)
  - `FenAfter` (string, position after move)
  - `EvaluationBefore` (int, centipawns before move)
  - `EvaluationAfter` (int, centipawns after move)
  - `EvaluationDelta` (int, change in evaluation)
  - `BestMove` (string, engine's recommended move)
  - `BestMoveEvaluation` (int, evaluation if best move was played)
  - `MoveQuality` (enum: Excellent, Good, Inaccuracy, Mistake, Blunder)
  - `IsBlunder` (bool)
  - `BlunderSeverity` (enum: None, Minor, Major, Critical)
- Create repository interface `IGameAnalysisRepository`
- Implement repository for chosen database (SQLite or Firestore)
- Add database migrations if using SQL
- Update `ChessGame` model to include optional `GameAnalysis` navigation property

**Key Integration Points:**
- Existing repository pattern
- Database context (if SQL) or Firestore service
- Model relationships

**Deliverables:**
- `GameAnalysis` model
- `MoveAnalysis` model
- `IGameAnalysisRepository` interface
- Repository implementation
- Database schema updates

### Phase 4: API Endpoints

**Agent should:**
- Create `AnalysisController` with endpoints:
  - `POST /api/analysis/{gameId}`: Trigger analysis for a game
  - `GET /api/analysis/{gameId}`: Get analysis results for a game
  - `GET /api/analysis/{gameId}/moves`: Get move-by-move analysis
  - `GET /api/analysis/{gameId}/blunders`: Get list of blunders
  - `GET /api/analysis/{gameId}/summary`: Get analysis summary
- Implement authorization checks (users can only analyze their own games)
- Add request/response DTOs:
  - `AnalyzeGameRequest` (optional analysis options)
  - `GameAnalysisResponse` (full analysis results)
  - `MoveAnalysisResponse` (move-by-move data)
  - `BlunderResponse` (blunder information)
  - `AnalysisSummaryResponse` (aggregated statistics)
- Implement async analysis processing:
  - Return analysis job ID immediately
  - Process analysis in background
  - Support polling or webhook for completion
  - Or implement synchronous analysis with progress updates (for shorter games)
- Add error handling:
  - Invalid game ID
  - Missing PGN content
  - Engine failures
  - Timeout handling for long games

**Key Integration Points:**
- `GameAnalysisService`
- `GameController` or existing game retrieval endpoints
- Authentication/authorization middleware
- Error response handling

**Deliverables:**
- `AnalysisController` implementation
- Request/response DTOs
- API documentation
- Error handling

### Phase 5: Frontend Service and Types

**Agent should:**
- Create `analysisService.ts` in `src/services/` directory
- Implement API client methods:
  - `analyzeGame(gameId: string, options?: AnalysisOptions): Promise<AnalysisJobResponse>`
  - `getAnalysis(gameId: string): Promise<GameAnalysis>`
  - `getMoveAnalyses(gameId: string): Promise<MoveAnalysis[]>`
  - `getBlunders(gameId: string): Promise<Blunder[]>`
  - `getAnalysisSummary(gameId: string): Promise<AnalysisSummary>`
  - `pollAnalysisStatus(jobId: string): Promise<AnalysisStatus>`
- Add TypeScript interfaces:
  - `GameAnalysis` (full analysis data)
  - `MoveAnalysis` (move-by-move data)
  - `Blunder` (blunder information)
  - `AnalysisSummary` (aggregated statistics)
  - `AnalysisOptions` (analysis configuration)
  - `AnalysisJobResponse` (job tracking)
  - `AnalysisStatus` (job status)
- Integrate with existing API configuration in `src/config/api.ts`
- Add error handling and retry logic for analysis requests

**Key Integration Points:**
- Existing API configuration
- Error handling patterns
- Type definitions

**Deliverables:**
- `analysisService.ts` with API client methods
- TypeScript interfaces and types
- Error handling utilities

### Phase 6: Frontend Analysis Display Components

**Agent should:**

#### 6.1 Create Analysis Summary Component
- Create `src/components/GameAnalysisSummary.tsx` component
- Display overall game statistics:
  - Total blunders, mistakes, inaccuracies count
  - Average evaluation
  - Game quality rating
  - Critical moments count
- Show visual indicators (badges, progress bars)
- Use existing UI components (Card, Badge, Progress)

#### 6.2 Create Move Analysis Component
- Create `src/components/MoveAnalysisList.tsx` component
- Display move-by-move analysis:
  - Move number and notation
  - Evaluation bar (visual representation of position evaluation)
  - Evaluation score (centipawns or pawns)
  - Move quality badge (color-coded: green=good, yellow=inaccuracy, red=blunder)
  - Best move suggestion (if different from played move)
  - Evaluation delta (how much position changed)
- Add interactive features:
  - Click to highlight move on board (if board component exists)
  - Expandable sections for detailed move information
  - Filter by move quality (show only blunders, etc.)
- Implement evaluation bar visualization:
  - Horizontal bar showing position evaluation
  - Color gradient (green for advantage, red for disadvantage)
  - Centered at 0 (equal position)

#### 6.3 Create Blunders Component
- Create `src/components/BlundersList.tsx` component
- Display list of blunders:
  - Move number and notation
  - Blunder severity (minor, major, critical)
  - Evaluation before and after
  - Best move suggestion
  - What went wrong (brief explanation)
- Add navigation to specific moves in game
- Show visual indicators for blunder severity

#### 6.4 Create Analysis Page
- Create `src/pages/GameAnalysis.tsx` page component
- Integrate all analysis components:
  - Analysis summary at top
  - Blunders list (if any)
  - Full move-by-move analysis
- Add analysis controls:
  - "Analyze Game" button (if analysis not yet performed)
  - Analysis status indicator (analyzing, completed, error)
  - Analysis options (depth, time limit)
- Add loading states and error handling
- Implement responsive design for mobile/tablet/desktop

#### 6.5 Update Game Display Components
- Modify `NotationDisplay.tsx` (or equivalent) to:
  - Show move quality indicators inline with moves
  - Add "View Analysis" button/link
  - Highlight blunders in move list
- Update game detail pages to include analysis section
- Add navigation from game view to analysis page

**Key Integration Points:**
- Existing `NotationDisplay` component
- Existing routing configuration
- Existing UI component library (shadcn/ui)
- Authentication context

**Deliverables:**
- `GameAnalysisSummary.tsx` component
- `MoveAnalysisList.tsx` component
- `BlundersList.tsx` component
- `GameAnalysis.tsx` page
- Updated `NotationDisplay.tsx` with analysis integration
- Routing configuration updates

### Phase 7: Testing

**Agent should create:**
- Unit tests for engine integration:
  - Engine initialization and communication
  - Position evaluation accuracy
  - Best move calculation
  - UCI protocol handling
- Unit tests for analysis service:
  - Move-by-move analysis logic
  - Blunder detection algorithms
  - Analysis result aggregation
  - Error handling for invalid PGN
- Integration tests:
  - Full game analysis flow
  - API endpoint testing
  - Database persistence of analysis results
- Frontend component tests:
  - Analysis display components
  - API service integration
  - Error state handling
- Performance tests:
  - Analysis time for games of various lengths
  - Engine resource usage
  - Concurrent analysis handling

**Deliverables:**
- Comprehensive test suite
- Test coverage for critical paths
- Performance benchmarks

## Technical Specifications

### Chess Engine Integration

**Recommended Approach:**
- Use Stockfish engine with UCI (Universal Chess Interface) protocol
- Implement UCI client in C# to communicate with Stockfish executable
- Alternative: Use Chess.NET library if it provides engine integration

**Engine Configuration:**
```csharp
public class EngineConfiguration
{
    public string EnginePath { get; set; } = "stockfish";
    public int DefaultDepth { get; set; } = 15;
    public int MaxAnalysisTimeMs { get; set; } = 5000;
    public int ThreadCount { get; set; } = 1;
    public int HashSizeMb { get; set; } = 64;
}
```

### Service Interface

```csharp
public interface IChessEngine
{
    Task<EngineEvaluation> EvaluatePositionAsync(string fen, int depth);
    Task<BestMoveResult> GetBestMoveAsync(string fen, int depth);
    Task<EngineInfo> GetEngineInfoAsync();
    void SetOption(string name, string value);
}

public interface IGameAnalysisService
{
    Task<GameAnalysis> AnalyzeGameAsync(Guid gameId, AnalysisOptions? options = null);
    Task<MoveAnalysis> AnalyzeMoveAsync(string pgn, int moveNumber);
    Task<List<Blunder>> DetectBlundersAsync(Guid gameId);
    Task<AnalysisSummary> GetAnalysisSummaryAsync(Guid gameId);
}
```

### Data Models

```csharp
public class GameAnalysis
{
    public Guid Id { get; set; }
    public Guid ChessGameId { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public string EngineVersion { get; set; } = string.Empty;
    public int AnalysisDepth { get; set; }
    public int AnalysisTimeMs { get; set; }
    public List<MoveAnalysis> MoveAnalyses { get; set; } = new();
    public AnalysisSummary Summary { get; set; } = new();
}

public class MoveAnalysis
{
    public int MoveNumber { get; set; }
    public string Move { get; set; } = string.Empty;
    public string FenBefore { get; set; } = string.Empty;
    public string FenAfter { get; set; } = string.Empty;
    public int EvaluationBefore { get; set; } // centipawns
    public int EvaluationAfter { get; set; } // centipawns
    public int EvaluationDelta { get; set; }
    public string? BestMove { get; set; }
    public int? BestMoveEvaluation { get; set; }
    public MoveQuality Quality { get; set; }
    public bool IsBlunder { get; set; }
    public BlunderSeverity BlunderSeverity { get; set; }
}

public enum MoveQuality
{
    Excellent,
    Good,
    Inaccuracy,
    Mistake,
    Blunder
}

public enum BlunderSeverity
{
    None,
    Minor,    // -200 to -500 centipawns
    Major,    // -500 to -1000 centipawns
    Critical  // < -1000 centipawns
}
```

### API Endpoints

```typescript
// Backend API endpoints:
POST /api/analysis/{gameId}              // Trigger analysis
  Request: { depth?: number, maxTimeMs?: number }
  Response: { jobId: string, status: string }

GET /api/analysis/{gameId}               // Get full analysis
  Response: GameAnalysis

GET /api/analysis/{gameId}/moves         // Get move-by-move analysis
  Response: MoveAnalysis[]

GET /api/analysis/{gameId}/blunders       // Get blunders list
  Response: Blunder[]

GET /api/analysis/{gameId}/summary        // Get analysis summary
  Response: AnalysisSummary

GET /api/analysis/job/{jobId}            // Get analysis job status
  Response: { status: string, progress?: number }
```

### Frontend TypeScript Interfaces

```typescript
// src/services/analysisService.ts
export interface GameAnalysis {
  id: string;
  gameId: string;
  analyzedAt: string;
  engineVersion: string;
  analysisDepth: number;
  analysisTimeMs: number;
  moves: MoveAnalysis[];
  summary: AnalysisSummary;
}

export interface MoveAnalysis {
  moveNumber: number;
  move: string;
  fenBefore: string;
  fenAfter: string;
  evaluationBefore: number; // centipawns
  evaluationAfter: number; // centipawns
  evaluationDelta: number;
  bestMove?: string;
  bestMoveEvaluation?: number;
  quality: 'excellent' | 'good' | 'inaccuracy' | 'mistake' | 'blunder';
  isBlunder: boolean;
  blunderSeverity: 'none' | 'minor' | 'major' | 'critical';
}

export interface Blunder {
  moveNumber: number;
  move: string;
  severity: 'minor' | 'major' | 'critical';
  evaluationBefore: number;
  evaluationAfter: number;
  bestMove: string;
  bestMoveEvaluation: number;
  description?: string;
}

export interface AnalysisSummary {
  totalMoves: number;
  blunders: number;
  mistakes: number;
  inaccuracies: number;
  averageEvaluation: number;
  gameQuality: 'excellent' | 'good' | 'average' | 'poor';
  criticalMoments: number;
}

export interface AnalysisOptions {
  depth?: number;
  maxTimeMs?: number;
}
```

### Frontend Component Structure

```
src/
  pages/
    GameAnalysis.tsx              // Main analysis page
  components/
    GameAnalysisSummary.tsx        // Summary statistics
    MoveAnalysisList.tsx           // Move-by-move analysis
    BlundersList.tsx               // Blunders list
    EvaluationBar.tsx              // Evaluation visualization
  services/
    analysisService.ts             // API client for analysis
```

### Frontend UI Requirements

- Analysis page should display:
  - Analysis summary card with key statistics
  - Blunders section (if any blunders detected)
  - Move-by-move analysis with evaluation bars
  - Best move suggestions for each move
  - Visual indicators for move quality (color-coded badges)
- Use existing design system:
  - shadcn/ui components (Card, Badge, Progress, Separator)
  - Tailwind CSS for styling
  - Responsive design for mobile/tablet/desktop
  - Consistent with existing game display patterns
- Evaluation visualization:
  - Horizontal bar showing position evaluation
  - Color gradient (green for advantage, red for disadvantage)
  - Centered at 0 (equal position)
  - Tooltip showing exact centipawn value

## Acceptance Criteria

### Backend
- [ ] Chess engine (Stockfish) integrated and functional
- [ ] `IChessEngine` interface implemented with UCI communication
- [ ] `IGameAnalysisService` implemented with move-by-move analysis
- [ ] Blunder detection algorithm correctly identifies blunders based on evaluation drops
- [ ] Analysis results stored in database with proper relationships
- [ ] API endpoints for analysis operations implemented
- [ ] Authorization checks ensure users can only analyze their own games
- [ ] Error handling for invalid PGN, engine failures, and timeouts
- [ ] Analysis can be triggered on-demand via API
- [ ] Unit tests for engine integration and analysis service
- [ ] Integration tests for full analysis flow
- [ ] Performance acceptable for games up to 100 moves (analysis completes in reasonable time)

### Frontend
- [ ] `analysisService.ts` created with API client methods
- [ ] TypeScript interfaces defined for all analysis data types
- [ ] `GameAnalysis.tsx` page displays full analysis
- [ ] `GameAnalysisSummary.tsx` shows game statistics
- [ ] `MoveAnalysisList.tsx` displays move-by-move analysis with evaluation bars
- [ ] `BlundersList.tsx` displays blunders with severity indicators
- [ ] Evaluation bars visualize position evaluations
- [ ] Move quality badges color-coded correctly
- [ ] "Analyze Game" button triggers analysis
- [ ] Analysis status indicator shows progress/status
- [ ] Navigation from game view to analysis page
- [ ] Inline move quality indicators in notation display
- [ ] Error handling for analysis failures
- [ ] Loading states during analysis
- [ ] Responsive design for mobile, tablet, and desktop
- [ ] UI follows existing design system and component patterns

## Dependencies

### Backend
- Existing `ChessGame` model and repository
- PGN parsing capability (may need to add library like PgnFSharp or similar)
- Chess position/FEN handling library
- File system or process management for Stockfish executable
- Configuration system for engine settings
- Logging infrastructure

### Frontend
- Existing game display components (`NotationDisplay`, etc.)
- Existing routing configuration
- Authentication context (`AuthContext`)
- UI component library (shadcn/ui components)
- Tailwind CSS for styling

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
Engine integration provides significant educational value and engagement features. Users can:
- Learn from their mistakes by seeing blunders and best moves
- Understand position evaluations and game quality
- Improve their chess skills through analysis
- Engage more deeply with their processed games

This feature enhances the product from a simple OCR tool to a comprehensive chess analysis platform, increasing user retention and providing a clear path to premium features (advanced analysis, deeper engine depth, etc.).

**GTM Alignment**:
- **Retention Feature**: Critical for Phase 1 retention rate (target: ≥30% users upload second game within 30 days)
- **User Value**: Educational value encourages users to return and analyze more games
- **Activation Support**: Analysis can be a "wow" feature that demonstrates product value
- **Monetization Path**: Premium tier can offer deeper analysis, multiple engine options, or faster analysis
- **Key Metrics to Track**:
  - % of users who analyze at least one game (target: ≥50%)
  - Average number of analyses per user
  - Time spent on analysis pages
  - User feedback on analysis quality

## Effort Estimation

**Effort Level**: 8

**Effort Breakdown**:

### Backend
- Chess engine research and selection: 2 hours
- Engine integration and UCI protocol implementation: 6 hours
- Analysis service implementation: 8 hours
- Blunder detection algorithm: 3 hours
- Data model design and implementation: 3 hours
- Repository implementation: 2 hours
- API endpoints implementation: 4 hours
- PGN parsing integration: 2 hours
- Error handling and edge cases: 3 hours
- Unit tests: 4 hours
- Integration tests: 3 hours
- **Backend Total**: 40 hours

### Frontend
- Analysis service and TypeScript interfaces: 2 hours
- GameAnalysisSummary component: 3 hours
- MoveAnalysisList component: 5 hours
- BlundersList component: 3 hours
- EvaluationBar visualization component: 4 hours
- GameAnalysis page integration: 3 hours
- Update NotationDisplay with analysis indicators: 3 hours
- Routing and navigation: 1 hour
- UI styling and responsive design: 4 hours
- Error handling and loading states: 2 hours
- Testing and refinement: 3 hours
- **Frontend Total**: 33 hours

**Total Estimated**: 73 hours

## Future Enhancements

This feature establishes the foundation for advanced chess analysis capabilities. Future enhancements may include:
- Multiple engine support (Stockfish, Leela Chess Zero, etc.)
- Deeper analysis options (higher depth, longer time limits)
- Position analysis (analyze specific positions from games)
- Opening book integration (identify openings and suggest theory)
- Endgame tablebase integration (perfect endgame play)
- Analysis comparison (compare multiple engine evaluations)
- Export analysis to PGN with annotations
- Interactive board with analysis overlay
- Analysis sharing and collaboration
- Historical analysis tracking (improvement over time)

