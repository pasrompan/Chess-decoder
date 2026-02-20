---
id: PG-198
status: completed
priority_score: 1.1428
effort: 7
impact: 8
dependencies: []
created_date: "2026-01-10"
updated_date: "2026-02-09"
plan_type: agent_plan
executable: false
---

# Implementation Plan: Stockfish Frontend Integration

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Integrate the Stockfish chess engine into the frontend React application to enable automated game analysis, position evaluation, and blunder detection. This provides the core engine capability required for the broader "Engine Integration for Game Analysis" feature (PG-197).

## Plan Overview

The system should:
1. Integrate a JavaScript/TypeScript Stockfish library (e.g., stockfish.js or stockfish.wasm) into the React frontend.
2. Create a React hook or service to manage engine lifecycle and analysis operations.
3. Develop UI components to display move-by-move evaluations, best moves, and blunder detection.
4. Integrate analysis results with the existing ChessBoard and NotationDisplay components.
5. Provide real-time analysis feedback as users navigate through game moves.

## Implementation Plan

### Phase 1: Stockfish Library Integration

**Agent should:**
- Install a Stockfish JavaScript/TypeScript library (e.g., `stockfish.js` or `stockfish.wasm`).
- Create a React hook (`useStockfish`) to manage engine initialization and lifecycle.
- Implement engine configuration (depth, threads, hash size) via React state or context.
- Handle engine loading and initialization in the browser.

**Key Integration Points:**
- React hooks for state management.
- Web Worker support for running Stockfish in background thread (if using stockfish.js).
- Error handling for engine initialization failures.

**Deliverables:**
- Stockfish library installed via npm.
- `useStockfish` hook for engine management.
- Configuration component/settings for engine parameters.

### Phase 2: Analysis Service Implementation

**Agent should:**
- Create a service/hook (`useGameAnalysis`) to coordinate analysis operations.
- Implement move-by-move analysis logic:
  - Convert PGN moves to FEN positions using `chess.js` (already in use).
  - Run engine evaluation on each position.
  - Extract centipawn scores and best moves from engine output.
- Cache analysis results to avoid re-analyzing the same positions.
- Handle analysis progress and completion states.

**Key Integration Points:**
- Existing `chess.js` library for position management.
- React state management for analysis results.
- Integration with existing `ChessBoard` component.

**Deliverables:**
- `useGameAnalysis` hook.
- Analysis result types/interfaces.
- Caching mechanism for analysis results.

### Phase 3: UI Components for Analysis Display

**Agent should:**
- Create components to display:
  - Move evaluation scores (centipawns) next to each move in NotationDisplay.
  - Best move suggestions with visual indicators.
  - Blunder detection (moves that significantly worsen position).
  - Overall game analysis summary.
- Add visual indicators (colors, icons) for:
  - Excellent moves (green)
  - Good moves (light green)
  - Inaccuracies (yellow)
  - Mistakes (orange)
  - Blunders (red)
- Integrate analysis display into existing NotationDisplay component.

**Key Integration Points:**
- Existing `NotationDisplay` component.
- UI component library (Radix UI, shadcn/ui).
- Color coding and visual feedback.

**Deliverables:**
- `AnalysisDisplay` component.
- `MoveEvaluation` component for individual move scores.
- `BlunderIndicator` component.
- Updated `NotationDisplay` with analysis integration.

### Phase 4: Integration and Testing

**Agent should:**
- Integrate analysis into the game viewing workflow.
- Add controls to start/stop analysis.
- Handle edge cases (incomplete games, invalid moves, etc.).
- Test performance with long games.
- Add loading states and progress indicators.

**Key Integration Points:**
- Complete game viewing flow.
- User interaction patterns.
- Performance optimization.

**Deliverables:**
- Fully integrated analysis feature.
- Loading and progress indicators.
- Error handling and edge case coverage.

## Technical Specifications

### useStockfish Hook
```typescript
interface UseStockfishReturn {
  engine: Stockfish | null;
  isReady: boolean;
  isLoading: boolean;
  error: string | null;
  analyzePosition: (fen: string, depth: number) => Promise<AnalysisResult>;
  getBestMove: (fen: string, depth: number) => Promise<string>;
  setDepth: (depth: number) => void;
}

function useStockfish(): UseStockfishReturn;
```

### Analysis Result Types
```typescript
interface AnalysisResult {
  evaluation: number; // centipawns (positive = white advantage)
  bestMove: string; // UCI format (e.g., "e2e4")
  depth: number;
  nodes?: number;
}

interface MoveAnalysis {
  moveNumber: number;
  move: string;
  evaluation: number;
  bestMove: string;
  isBlunder: boolean;
  isMistake: boolean;
  isInaccuracy: boolean;
}
```

### UI Component Integration
- Add analysis indicators to `NotationDisplay` component
- Show evaluation scores inline with moves
- Display best move suggestions on hover/click
- Color-code moves based on quality

## Acceptance Criteria

### Frontend
- [ ] Stockfish library is installed and properly initialized in the React app.
- [ ] Engine correctly analyzes positions and returns evaluations and best moves.
- [ ] Analysis results are displayed inline with moves in the NotationDisplay component.
- [ ] Visual indicators (colors, icons) show move quality (excellent, good, inaccuracy, mistake, blunder).
- [ ] Best move suggestions are displayed and accessible to users.
- [ ] Analysis can be started/stopped by user action.
- [ ] Loading states and progress indicators are shown during analysis.
- [ ] Analysis results are cached to avoid re-analyzing the same positions.
- [ ] Performance is acceptable for games up to 100 moves.

## Dependencies

### Frontend
- Stockfish JavaScript library (e.g., `stockfish.js` or `stockfish.wasm`).
- Existing `chess.js` library (already installed).
- Existing `react-chessboard` component (already installed).
- React hooks for state management.

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
Integrating Stockfish is the foundational step for providing move-by-move analysis, which is one of the most requested features for improving chess skills. It transforms the app from a simple digitizer into a powerful analysis tool.

## Effort Estimation

**Effort Level**: 7

**Effort Breakdown**:
- Library Integration & Setup: 3 hours
- React Hooks & Analysis Service: 8 hours
- UI Components for Analysis Display: 10 hours
- Integration with Existing Components: 6 hours
- Testing & Performance Optimization: 5 hours
- **Frontend Total**: 32 hours

**Total Estimated**: 32 hours
