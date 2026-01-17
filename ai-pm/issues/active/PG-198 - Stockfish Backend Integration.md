---
id: PG-198
status: active
priority_score: 1.1428
effort: 7
impact: 8
dependencies: []
created_date: "2026-01-10"
updated_date: 2026-01-10
plan_type: agent_plan
executable: false
---

# Implementation Plan: Stockfish Backend Integration

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Integrate the Stockfish chess engine into the backend infrastructure to enable automated game analysis, position evaluation, and blunder detection. This provides the core engine capability required for the broader "Engine Integration for Game Analysis" feature (PG-197).

## Plan Overview

The system should:
1. Integrate the Stockfish executable into the backend deployment and environment.
2. Implement a robust UCI (Universal Chess Interface) protocol handler for communication.
3. Create an abstraction layer (`IChessEngine`) to manage engine lifecycle and commands.
4. Develop a game analysis service that can process PGNs move-by-move.
5. Provide high-performance analysis while managing system resources effectively.

## Implementation Plan

### Phase 1: Infrastructure and Setup

**Agent should:**
- Download and bundle the appropriate Stockfish executable for the target environment.
- Configure `appsettings.json` with engine paths, default depth, and resource limits.
- Set up process management to handle starting, stopping, and restarting the engine process.
- Implement a basic UCI command sender and receiver.

**Key Integration Points:**
- Backend configuration system.
- System process management (System.Diagnostics.Process).
- Dockerfile/deployment scripts for bundling the executable.

**Deliverables:**
- Stockfish executable in the project structure.
- Configuration settings in `appsettings.json`.
- Basic process wrapper for the engine.

### Phase 2: UCI Protocol Wrapper Implementation

**Agent should:**
- Implement the `IChessEngine` interface with support for:
  - `isready`, `ucinewgame`, `position`, `go`, `stop`, `quit` commands.
  - Parsing engine output (bestmove, info depth, info score cp, etc.).
- Add support for setting engine options (Threads, Hash, MultiPV).
- Implement timeout handling and error recovery for unresponsive engine processes.
- Ensure thread-safety for concurrent analysis requests (or implement a pooling mechanism).

**Key Integration Points:**
- Dependency Injection (as a singleton or scoped service).
- Logging for UCI communication debugging.

**Deliverables:**
- `IChessEngine` interface.
- `StockfishEngine` implementation.
- Unit tests for UCI parsing and command handling.

### Phase 3: Analysis Service and API

**Agent should:**
- Create `IGameAnalysisService` to provide high-level analysis functions.
- Implement move-by-move analysis logic:
  - Parse PGN to FEN sequence.
  - Run engine evaluation on each FEN.
  - Extract centipawn scores and best moves.
- Create API endpoints in a new `AnalysisController` to:
  - Trigger analysis for a specific game ID.
  - Retrieve current analysis status or results.

**Key Integration Points:**
- Existing `GameController` and `ChessGame` repositories.
- PGN parsing logic.

**Deliverables:**
- `IGameAnalysisService` implementation.
- `AnalysisController` with core endpoints.
- Analysis result DTOs.

### Phase 4: Persistence and Testing

**Agent should:**
- Extend the data model to store analysis results (MoveAnalysis, GameAnalysis).
- Implement database persistence using existing repository patterns.
- Create comprehensive integration tests covering the full flow from PGN to stored analysis.
- Verify performance and resource usage with multiple concurrent analysis requests.

**Key Integration Points:**
- Database context and migrations.
- Test suite infrastructure.

**Deliverables:**
- Database schema updates.
- Repository methods for analysis persistence.
- Integration test suite.

## Technical Specifications

### IChessEngine Interface
```csharp
public interface IChessEngine
{
    Task<string> GetBestMoveAsync(string fen, int depth);
    Task<int> GetEvaluationAsync(string fen, int depth); // centipawns
    Task SetOptionAsync(string name, string value);
    Task InitializeAsync();
}
```

### API Endpoints
```typescript
// Backend API endpoints to implement:
POST /api/analysis/{gameId}
  Request: { depth: number }
  Response: { status: "Started", jobId: string }

GET /api/analysis/{gameId}
  Response: GameAnalysisDto
```

## Acceptance Criteria

### Backend
- [ ] Stockfish executable is correctly bundled and accessible by the application.
- [ ] UCI protocol handler correctly parses evaluation and bestmove output.
- [ ] Engine process is automatically restarted if it crashes or hangs.
- [ ] Game analysis service correctly processes a full PGN and returns evaluations for every move.
- [ ] Analysis results are persisted in the database and linked to the correct game.
- [ ] API provides clear feedback on analysis progress and completion.

## Dependencies

### Backend
- Stockfish 16+ executable.
- Existing `ChessGame` repository and database.
- `System.Diagnostics.Process` for engine interaction.

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
Integrating Stockfish is the foundational step for providing move-by-move analysis, which is one of the most requested features for improving chess skills. It transforms the app from a simple digitizer into a powerful analysis tool.

## Effort Estimation

**Effort Level**: 7

**Effort Breakdown**:
- Infrastructure & Setup: 4 hours
- UCI Wrapper: 10 hours
- Analysis Service: 8 hours
- API & Persistence: 8 hours
- Testing: 6 hours
- **Backend Total**: 36 hours

**Total Estimated**: 36 hours
