---
id: PG-202
status: active
priority_score: 3.0000
effort: 2
impact: 6
dependencies: []
created_date: "2026-04-25"
updated_date: 2026-04-25
plan_type: agent_plan
executable: false
---

# Implementation Plan: Curation-vs-Explore Mode Toggle and Curation Gate (v1)

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Users frequently start playing **chess variants** on the interactive board (clicking around to try “what if” lines) **before** they finish curating the OCR’d game — i.e. before they correct invalid moves, validate the result, or confirm the final move list. Today board clicks call `onManualMove` → `handleManualMove` in `NotationDisplay.tsx`, which **mutates the curated move list**, so exploration corrupts the curation work and users never come back to validate.

This v1 ticket ships the **minimum behavior change** to fix that bug:
1. A clear **Mode toggle** between `Curation` (default, today’s behavior) and `Explore` (clicks do **not** mutate the move list).
2. A **Curation Gate** that prompts the user to fix unresolved issues before entering Explore mode or exporting / marking processing complete.

The richer **variant tree, persistence, panel UI, and PGN-with-variants export** are tracked separately in **PG-203** and depend on this ticket.

## Plan Overview

The system should:
1. Add a `boardMode: 'curation' | 'explore'` state in `NotationDisplay.tsx`, default `'curation'`.
2. Add a UI toggle near the board controls (shadcn `ToggleGroup`) with clear labels and tooltips.
3. Pass `mode` into `ChessBoard.tsx`. In `explore` mode, **suppress** `onManualMove` so clicks do not change `editedMoves`. (The board may still let the user move pieces locally for visual exploration, but no mutation escapes the component in v1.)
4. Add a **Curation Gate** dialog that opens when the user:
   - switches to `Explore`, OR
   - clicks **Export PGN** / **Mark Processing Complete**
   while unresolved issues remain.
5. Visually distinguish the two modes (badge / border / banner) so the user always knows what clicks will do.

## Implementation Plan

### Phase 1: Mode toggle + ChessBoard wiring

**Agent should:**
- Add `boardMode` state in `NotationDisplay.tsx`.
- Add a toggle UI near the existing board controls (next to navigation / auto-play).
- Add an optional `mode?: 'curation' | 'explore'` prop on `ChessBoard.tsx` (default `'curation'`).
- In `ChessBoard.tsx`, gate the call to `onManualMove?.(...)` so it only fires when `mode === 'curation'`.
- Verify navigation, validation, and auto-play still behave identically in both modes.

**Key Integration Points:**
- `chess-scribe-convert/src/components/NotationDisplay.tsx` (around `handleManualMove`, board render at ~line 1409).
- `chess-scribe-convert/src/components/ChessBoard.tsx` (`onManualMove` call site).

**Deliverables:**
- Toggle visible and functional.
- In Explore mode, no mutation reaches `editedMoves` / curated PGN.

### Phase 2: Curation Gate dialog

**Agent should:**
- Create `CurationGateDialog` (shadcn `Dialog`) that lists unresolved issues:
  - `firstInvalidMoveIndex !== -1` → invalid move present (with ply + cell).
  - Result is empty / `*` when game has > N moves (configurable, e.g. N = 5).
  - Player names empty (soft warning, not blocking).
- Each issue should include a “Jump to” action that scrolls/focuses the relevant cell (use existing focus state in `NotationDisplay`).
- Footer buttons: **Fix now** (close, focus first issue) and **Continue anyway** (close, proceed with the action that opened it).
- Wire the dialog into:
  - The mode toggle (when switching to `Explore`).
  - **Export PGN** buttons.
  - `handleMarkProcessingComplete`.

**Key Integration Points:**
- `chess-scribe-convert/src/components/CurationGateDialog.tsx` (new).
- `chess-scribe-convert/src/components/NotationDisplay.tsx` (mode switch handler, export buttons section ~line 1428, `handleMarkProcessingComplete` ~line 1050).

**Deliverables:**
- Reusable dialog component.
- Single source of truth for “unresolved issues” list (a small helper function returning `CurationIssue[]`).

### Phase 3: Visual mode signal + nudges

**Agent should:**
- In Explore mode, add a small badge near the board (e.g. *“Exploration mode — moves are not saved to the game”*).
- In Curation mode with unresolved issues, show a count badge on the mode toggle (e.g. red dot with the number of invalid moves).
- Decide once and document: **Export PGN** and **Mark Processing Complete** should route through the gate (default), not be hard-disabled.

**Deliverables:**
- Clear visual mode signal.
- Curation-first nudges baked into the existing actions.

## Technical Specifications

### Component prop additions

```ts
mode?: 'curation' | 'explore';
```

### Curation issue type (shared helper)

```ts
type CurationIssue =
  | { kind: 'invalid_move'; ply: number; cell: 'white' | 'black' }
  | { kind: 'missing_result' }
  | { kind: 'missing_player'; side: 'white' | 'black' };

function collectCurationIssues(state: {
  firstInvalidMoveIndex: number;
  result: string | undefined;
  whitePlayer: string;
  blackPlayer: string;
  movesCount: number;
}): CurationIssue[] { /* ... */ }
```

### File touch list (expected)

```
chess-scribe-convert/src/components/NotationDisplay.tsx
chess-scribe-convert/src/components/ChessBoard.tsx
chess-scribe-convert/src/components/CurationGateDialog.tsx   (new)
chess-scribe-convert/src/utils/curationIssues.ts             (new, shared helper)
```

## Acceptance Criteria

### Backend

- [ ] No backend change required.

### Frontend

- [ ] User can switch between **Curation** and **Explore** modes from a clearly labeled toggle near the board.
- [ ] In Curation mode: clicking pieces still updates the move table as it does today (no regression to `handleManualMove`).
- [ ] In Explore mode: clicks **never** mutate `editedMoves` / curated PGN (verified by a unit/integration test or manual checklist).
- [ ] Switching to Explore mode (or clicking **Export PGN** / **Mark Processing Complete**) with unresolved issues opens the **Curation Gate** with actionable items and a **Continue anyway** path.
- [ ] Mode is visually obvious (badge/border/banner).
- [ ] Build passes (`npm run build`); existing tests still pass.

## Dependencies

### Backend

- None.

### Frontend

- Existing `ChessBoard`, `NotationDisplay`, validation pipeline (`firstInvalidMoveIndex`, `validMovesCount`).
- Existing `processingCompleted` flag and `handleMarkProcessingComplete` in `NotationDisplay.tsx`.
- shadcn `Dialog`, `ToggleGroup`, `Badge`.

## Impact Assessment

**Impact Level**: High (relative to effort)

**Impact Description**: Stops a recurring data-quality bug where exploration overwrites curation, and creates the foundation that PG-203 builds on. Low risk because the change is mostly additive: a mode flag, a gate dialog, and a guarded `onManualMove` call.

## Effort Estimation

**Effort Level**: 2 (1–10)

**Effort Breakdown**:

### Backend

- 0 hours.

### Frontend

- Mode toggle + ChessBoard wiring: 2 h
- Curation Gate dialog + issue helper + integration with export/complete: 3 h
- Visual mode signal + nudges + tests: 2 h
- **Frontend Total**: ~7 hours

**Total Estimated**: ~7 hours

## Future Enhancements

- Tracked in **PG-203 — Variant Tree, Persistence, and PGN Export with Variations**:
  - Real variant tree data model and `useVariants` hook.
  - Persistence per game (local first, backend follow-up).
  - Variants panel UI with navigation, rename, delete.
  - PGN export with `(...)` variations.
  - Promote-variant-to-main-line, engine annotations per variant, onboarding tooltips.
