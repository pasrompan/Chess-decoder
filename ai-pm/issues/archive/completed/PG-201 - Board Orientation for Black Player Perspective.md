---
id: PG-201
status: active
priority_score: 1.6666
effort: 3
impact: 5
dependencies: []
created_date: "2026-04-25"
updated_date: 2026-04-25
plan_type: agent_plan
executable: false
---

# Implementation Plan: Board Orientation for Black Player Perspective

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Chess boards in apps are usually rendered with White at the bottom and Black at the top. Users who transcribed or care about the game from **Black’s seat** expect the board flipped so their pieces and perspective match a physical board in front of them. Today the interactive board in `chess-scribe-convert` uses `react-chessboard` without `boardOrientation`, so it always shows the White-on-bottom default. This issue adds a clear way to view (and optionally default) the board from Black’s angle.

## Plan Overview

The system should:
1. Support `boardOrientation="black"` on the `Chessboard` component (and keep `"white"` as default where appropriate).
2. Expose orientation to the user: toggle (flip board), and/or infer default from metadata when reliable (e.g. user indicates they played Black, or future PGN tag if added).
3. Keep move entry, FEN, and `chess.js` logic unchanged; only the **visual** representation flips (square labels and piece placement follow orientation).
4. Align the **evaluation bar** (when shown) with the flipped perspective so “good for bottom player” still matches what the user sees at the bottom of the board.
5. Persist orientation in session or UI state as needed so users are not reset on every navigation.

## Implementation Plan

### Phase 1: Core board flip

**Agent should:**
- Add optional prop on `ChessBoard` (e.g. `boardOrientation?: 'white' | 'black'`) defaulting to `'white'`.
- Pass `boardOrientation` through to `react-chessboard`’s `Chessboard` in `ChessBoard.tsx` (see current usage around the main `<Chessboard />` render).
- Verify `onSquareClick` / square names still map correctly with orientation (library should handle this; confirm in manual test).

**Key Integration Points:**
- `chess-scribe-convert/src/components/ChessBoard.tsx`
- `react-chessboard` API: `boardOrientation`

**Deliverables:**
- Flippable board controlled by prop.

### Phase 2: User control and wiring from NotationDisplay

**Agent should:**
- Add UI control: e.g. “Flip board” toggle or icon button near existing board controls (next to auto-play / navigation).
- Lift state in `NotationDisplay` (or parent) if the control lives outside `ChessBoard`, or keep state inside `ChessBoard` if self-contained.
- Optionally: default orientation to Black when the user has set themselves as Black in upload/metadata flows (`ImageUpload` already has White/Black side hints—evaluate whether that metadata reaches `NotationDisplay` and can set initial orientation).

**Key Integration Points:**
- `chess-scribe-convert/src/components/NotationDisplay.tsx`
- `chess-scribe-convert/src/components/ImageUpload.tsx` (metadata / side if available)

**Deliverables:**
- User-visible flip control and stable state.

### Phase 3: Evaluation bar alignment

**Agent should:**
- Review `EvaluationBar` and `useGameAnalysis` normalization (eval is stored from White’s perspective in places). When `boardOrientation` is `black`, invert or remap the bar so positive eval toward the bottom of the **screen** matches the player at the bottom (Black).
- Add tests or storybook-style manual checklist for: White bottom + bar, Black bottom + bar.

**Key Integration Points:**
- `chess-scribe-convert/src/components/EvaluationBar.tsx`
- `chess-scribe-convert/src/hooks/useGameAnalysis.ts` (if bar reads raw eval)

**Deliverables:**
- Evaluation bar visually consistent with board orientation.

### Phase 4: Testing and polish

**Agent should:**
- Run `npm run build` in `chess-scribe-convert`.
- Add or extend component tests if the project uses them for `ChessBoard`.
- Keyboard/accessibility: ensure flip control has `title` / `aria-label`.

**Deliverables:**
- Green build; documented acceptance criteria checked.

## Technical Specifications

### react-chessboard

```tsx
<Chessboard
  boardOrientation={boardOrientation}
  // ...existing props
/>
```

### Component API (proposed)

```typescript
// ChessBoardProps extension
boardOrientation?: 'white' | 'black';
onBoardOrientationChange?: (orientation: 'white' | 'black') => void; // optional, for lifted state
```

### File touch list (expected)

```
chess-scribe-convert/src/components/ChessBoard.tsx
chess-scribe-convert/src/components/NotationDisplay.tsx
chess-scribe-convert/src/components/EvaluationBar.tsx  // if bar needs inversion prop
```

## Acceptance Criteria

### Backend

- [ ] No backend change required unless PGN headers for viewer preference are added later (out of scope unless product asks).

### Frontend

- [ ] User can flip the board between White-on-bottom and Black-on-bottom.
- [ ] All legal interactions (click-to-move, navigation, auto-play) behave correctly in both orientations.
- [ ] With Stockfish / evaluation bar enabled, the bar reflects the same bottom player as the board (no misleading “white good” at Black’s bottom edge without inversion).
- [ ] Default remains White-on-bottom unless product adds auto-default from metadata (optional sub-criterion).
- [ ] Build passes (`npm run build`).

## Dependencies

### Backend

- None for minimal flip + toggle.

### Frontend

- `react-chessboard` (existing)
- `ChessBoard`, `NotationDisplay`, optional `EvaluationBar` / `useGameAnalysis`

## Impact Assessment

**Impact Level**: Medium

**Impact Description**: Improves usability for a large segment of users (anyone reviewing games as Black). Low risk if limited to visual `boardOrientation` and eval bar consistency.

## Effort Estimation

**Effort Level**: 3 (1–10)

**Effort Breakdown**:

### Backend

- None: 0 hours

### Frontend

- Prop + `Chessboard` wiring + toggle UI: 2–3 hours
- Evaluation bar alignment + testing: 2–3 hours

**Total Estimated**: ~4–6 hours

## Future Enhancements

- Persist `boardOrientation` in localStorage or project settings.
- PGN tag or user profile preference: `[Orientation "black"]` or app-specific metadata (only if standardized).
- Flip coordinates / rank-file labels if the design adds algebraic labels around the board.
