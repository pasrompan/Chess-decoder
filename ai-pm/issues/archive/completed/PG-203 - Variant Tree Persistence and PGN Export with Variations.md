---
id: PG-203
status: completed
priority_score: .7000
effort: 5
impact: 6
dependencies: ["PG-202"]
created_date: "2026-04-25"
updated_date: 2026-04-25
plan_type: agent_plan
executable: false
---

# Implementation Plan: Variant Tree, Persistence, and PGN Export with Variations

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Builds on **PG-202** (Curation-vs-Explore mode toggle and Curation Gate). Once Explore mode exists, this ticket makes it actually useful by:

1. Capturing every move played in Explore mode as a node in a **variant tree** rooted at the current ply of the curated game.
2. Persisting variants per game so they survive reload (frontend storage first; backend follow-up tracked here).
3. Showing variants in a **Variants panel** with navigation, rename, and delete.
4. Letting the user **export PGN with variations** using standard `(...)` syntax, so analysis work can be opened in Lichess / Chessbase / etc.

The curated main line and PGN are **never** modified by variants in v1 — that’s a deliberate scope line.

## Plan Overview

The system should:
1. Define a variant tree data model (`VariantNode` + lookup maps), keyed by game.
2. Add a `useVariants(gameId)` hook that owns reads/writes and persists state.
3. Replace PG-202’s “suppress mutations” behavior in Explore mode with a real `onVariantMove` callback that appends nodes.
4. Render a `VariantsPanel` next to / under the board, grouped by branch ply.
5. Add a “**Export PGN with variants**” action that emits standard PGN with `(...)` variations.
6. Persist variants in the existing per-game frontend storage (`projectService` / local storage). Backend persistence is a follow-up phase, optional in this ticket.

## Implementation Plan

### Phase 1: Data model + hook

**Agent should:**
- Add types:
  ```ts
  type VariantNode = {
    id: string;            // uuid
    parentId: string | null;
    rootPly: number;       // ply in curated main line where this branch starts
    rootFen: string;       // FEN at branch point (snapshot for safety)
    san: string;           // SAN move that creates this node
    children: string[];
    createdAt: string;
    updatedAt: string;
    label?: string;
  };

  type GameVariants = {
    nodes: Record<string, VariantNode>;
    rootsByPly: Record<number, string[]>;
  };
  ```
- Implement `src/hooks/useVariants.ts`:
  - `appendMove(parentVariantId | null, rootPly, fen, san) => string` (returns new node id).
  - `goToNode(id)`, `renameNode(id, label)`, `deleteNode(id)` (cascades to children).
  - `currentPath` (string[] of node ids representing the line currently on the board).
  - `exportPgnWithVariants(mainPgn) => string`.

**Deliverables:**
- Hook with unit tests for append / delete (cascades) / export.

### Phase 2: Wire Explore mode → variant tree

**Agent should:**
- In `ChessBoard.tsx`, add `onVariantMove?: (parentVariantId: string | null, rootPly: number, fen: string, san: string) => void` and call it in Explore mode when a legal move is played.
- In `NotationDisplay.tsx`, connect `onVariantMove` to `useVariants.appendMove`.
- Track active variant cursor (which node id, plus depth from its root) so the user can keep playing within the variant.

**Deliverables:**
- Playing in Explore mode produces a tree of `VariantNode`s in memory.

### Phase 3: Persistence

**Agent should:**
- Extend the per-game frontend storage used by `projectService.ts` / local storage layer (`tests/unit/localStorageGames.test.ts` shows the current shape) to include `variants: GameVariants`.
- Hydrate on game open / refresh. Reset cursor to “no variant” by default.
- Ensure existing PGN export (without variants) is unchanged.

**Deliverables:**
- Variants survive reload.
- No regression in existing exports or game list pages (`Projects.tsx`, `Project.tsx`).

### Phase 4: Variants panel UI

**Agent should:**
- Add `VariantsPanel` (collapsible, next to or under the board). Group by branch ply, e.g.:
  ```
  After 14...Nf6
    ├─ 15. Bxh7+ Kxh7 16. Ng5+   (Greek gift)
    └─ 15. Re1
  ```
- Clicking a node loads that line on the board (still in Explore mode).
- Per-node actions: rename, delete, **Copy line as PGN**.
- Empty state: short hint pointing at the Explore toggle.

**Deliverables:**
- Working tree UI with rename/delete/copy.

### Phase 5: PGN export with variations

**Agent should:**
- Implement `exportPgnWithVariants(mainPgn)` using standard `(...)` PGN syntax for variations rooted at the right ply.
- Add a new export button next to the existing PGN export(s).
- Tests: round-trip a small fixture (main line + 1–2 variants) through a PGN parser to confirm it’s valid.

**Deliverables:**
- Working with-variants export, behind a clearly labeled button.

### Phase 6 (delivered): Backend persistence

**Implemented:**
- New nullable `VariantsJson` column on `ChessGames` (SQLite migration `20260425120000_AddChessGameVariantsJson` + `[FirestoreProperty]` so Firestore picks it up automatically); model snapshot updated.
- `GameDetailsResponse.VariantsJson` returned from `GET /api/Game/{gameId}` via `MapToGameDetailsResponse`.
- Dedicated endpoint `PUT /api/Game/{gameId}/variants?userId=...` with body `{ variantsJson: string | null }`. Mirrors the auth pattern of `/pgn` and `/complete` (ownership checked against `userId`; mismatched / missing game returns 404). Whitespace payloads clear the field; idempotent against unchanged JSON.
- Frontend `imageService.updateGameVariants()` + `useVariants({ serverVariantsJson, onPersist })` reconciliation:
  - localStorage hydrates instantly so the panel is never empty offline.
  - When the server payload arrives, non-empty server state wins; empty server state preserves any local-only data which then gets PUT on the next change.
  - Saves debounced (default 800ms) and skipped when payload is byte-identical to the last successful PUT; pending save is flushed on unmount.
- Backend tests: `GameControllerTests` (5 new) + `GameManagementServiceTests` (5 new). Frontend test: `tests/unit/updateGameVariants.test.ts` (5 cases). All 240 backend / 40 frontend tests pass.

## Technical Specifications

### Component prop additions

```ts
// ChessBoard.tsx (added on top of PG-202's `mode`)
onVariantMove?: (parentVariantId: string | null, rootPly: number, fen: string, san: string) => void;
```

### Hook surface

```ts
export function useVariants(gameId: string) {
  return {
    variants,            // GameVariants
    currentPath,         // string[]
    appendMove,
    goToNode,
    renameNode,
    deleteNode,
    exportPgnWithVariants, // (mainPgn: string) => string
  };
}
```

### File touch list (expected)

```
chess-scribe-convert/src/hooks/useVariants.ts                (new)
chess-scribe-convert/src/components/VariantsPanel.tsx        (new)
chess-scribe-convert/src/components/ChessBoard.tsx           (add onVariantMove)
chess-scribe-convert/src/components/NotationDisplay.tsx      (panel + export wiring)
chess-scribe-convert/src/services/projectService.ts          (persist variants)
chess-scribe-convert/src/utils/pgnVariations.ts              (new, export helper)
chess-scribe-convert/tests/unit/useVariants.test.ts          (new)
```

## Acceptance Criteria

### Backend

- [ ] Optional in v1. If included: variants persist server-side and round-trip through the API; otherwise, an explicit follow-up ticket exists.

### Frontend

- [ ] Playing moves in Explore mode appends nodes to a variant tree without ever mutating `editedMoves` / curated PGN.
- [ ] Variants persist across page reload for the same game.
- [ ] Variants panel renders the tree, supports navigation, rename, and delete (with cascade).
- [ ] **Export PGN with variants** produces a valid PGN string parseable by `chess.js` (or a known parser fixture in tests).
- [ ] Existing main-line PGN export is unchanged.
- [ ] Build passes (`npm run build`); existing tests still pass; new unit tests for `useVariants` and PGN export pass.

## Dependencies

### Backend

- None unless Phase 6 is included.

### Frontend

- **PG-202** (mode toggle + Curation Gate must exist; Explore mode is the entry point for variant moves).
- Existing `ChessBoard`, `NotationDisplay`, `projectService`, local storage layer, `chess.js`.

## Impact Assessment

**Impact Level**: Medium-High

**Impact Description**: Turns Explore mode from “a safe sandbox that forgets” into a real analysis feature: persisted variants and PGN-with-variations export. Most of the user-flow data-quality fix already lands in PG-202; this ticket adds the analytical value users were trying to get when they accidentally broke the curation flow.

## Effort Estimation

**Effort Level**: 5 (1–10)

**Effort Breakdown**:

### Backend

- 0 hours unless Phase 6 is included (then ~4–6 hours).

### Frontend

- Variant data model + `useVariants` hook + tests: 4 h
- Wire Explore mode → tree: 2 h
- Persistence (frontend storage): 3 h
- Variants panel UI: 4 h
- PGN export with variations + tests: 3 h
- Polish + manual QA: 2 h
- **Frontend Total**: ~18 hours

**Total Estimated**: ~18 hours (frontend-only v1)

## Future Enhancements

- **Promote variant to main line** (replace curated continuation from a chosen ply with a variant).
- **Engine annotation per variant** (Stockfish on demand, store evals on nodes).
- **Backend persistence + sync** (Phase 6 spun out into its own ticket if not included here).
- **Onboarding tooltip** the first time a user enters Explore mode.
- **Color-by-result** highlights on variant nodes once engine evals are available.
