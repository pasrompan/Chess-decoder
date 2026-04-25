---
id: PG-204
status: active
priority_score: 2.6666
effort: 3
impact: 8
dependencies: []
created_date: "2026-04-25"
updated_date: "2026-04-25"
plan_type: agent_plan
executable: false
---

# Implementation Plan: Ungated Trial Mode with Sample Image and Mock API

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Today the landing page (`/`) is wrapped in `ProtectedRoute`, so any unauthenticated visitor is bounced to `/signin` before they can see how the product works. Users don’t understand the product until they’ve already signed up — a major activation problem.

Add an **ungated trial experience** that:
1. Lets a first-time visitor see the **whole flow** (image → OCR → editable PGN → board) **without signing in**.
2. Auto-loads one of the existing **sample chess scoresheet images** (`/sample-images/...`).
3. Toggles the **mock API endpoint** (`/api/mock/upload`) for the trial session so we don’t spend OCR credits or hit the database on anonymous traffic.
4. Lets the user explore the result (navigate moves, play through, see PGN), but **gates uploading their own file, saving, exporting to projects, marking complete** — those CTAs route to `/signin` with a return path so they continue seamlessly after auth.

The plumbing is already partially in place:
- Sample images live under `chess-scribe-convert/public/sample-images/` and are loaded by `handleSampleImage` in `ImageUpload.tsx`.
- The mock API switch already exists: `useMockApi()` in `src/services/imageService.ts` reads `localStorage.chessDecoder_useMockApi` (priority) or `VITE_USE_MOCK_API`. Mock endpoint is `/api/mock/upload`.
- Auth state is in `AuthContext` (`isAuthenticated`).

## Plan Overview

The system should:
1. Add a public route `/try` (and link to it from `/signin`) that renders a **TrialMode** page wrapping the existing `Index` flow without `ProtectedRoute`.
2. On entering `/try`, set `localStorage.chessDecoder_useMockApi = 'true'` for the duration of the trial, and **clear** it on exit / sign-in / leave.
3. Auto-load a sample image immediately (reuse `handleSampleImage` logic, lifted into a shared util) so the user lands on a populated screen and can scrub through the experience.
4. Keep all read-only / in-page interactions enabled (navigate moves, auto-play, board, PGN preview, copy to clipboard).
5. Gate write/persistent actions behind sign-in:
   - Custom file upload
   - Save to projects / “Mark processing complete”
   - Export to Lichess **with account** (a generic “Open in Lichess” link that doesn’t require auth can stay)
   - Continuation/dual-page upload
6. Add a **persistent “Try with your own image — Sign in” CTA** (sticky banner or top-of-page card) that explains the trial scope.

## Implementation Plan

### Phase 1: Public route + trial entry

**Agent should:**
- Add a new route `/try` in `src/App.tsx` that is **not** wrapped in `ProtectedRoute`.
- Create `src/pages/Trial.tsx` that renders the `Index`-style layout (Header + main + footer) but in trial mode.
- In `src/pages/SignIn.tsx`, add a clearly visible secondary action: **“Try without signing in”** → links to `/try`.
- On `Header`, when unauthenticated and on `/try`, replace user-only links (My Games, Profile) with a primary **“Sign in”** CTA.

**Key Integration Points:**
- `chess-scribe-convert/src/App.tsx`
- `chess-scribe-convert/src/pages/SignIn.tsx`
- `chess-scribe-convert/src/components/Header.tsx`

**Deliverables:**
- `/try` reachable without auth.
- Entry points from `/signin` and (optionally) the unauth redirect.

### Phase 2: Mock API toggle scoped to the trial session

**Agent should:**
- Create a small `useTrialMode()` hook (or set in `Trial.tsx` via `useEffect`) that:
  - On mount: `localStorage.setItem('chessDecoder_useMockApi', 'true')` and remembers the previous value.
  - On unmount: restore previous value (or `removeItem` if there wasn’t one).
- Also provide a `TrialModeContext` that exposes `isTrial: boolean` so child components (Header, ImageUpload, NotationDisplay) can adjust UX.
- Confirm `imageService.useMockApi()` picks up the runtime override (it already does — see `src/services/imageService.ts` lines 6–16).

**Deliverables:**
- Mock endpoint hit only inside `/try`.
- Real endpoint restored on leaving the page (especially if user signs in mid-flow).

### Phase 3: Auto-load sample image

**Agent should:**
- Extract `handleSampleImage` selection + loading logic from `ImageUpload.tsx` into a shared util `src/utils/sampleImage.ts` (returns a `File` + display name). Keep the existing `ImageUpload` button working through the util.
- In `Trial.tsx`, on mount, auto-load a sample image and feed it into the existing `ImageUpload` → `handleImageProcessed` pipeline so the trial page starts already mid-experience.
- Add a small **“Try a different sample”** button in trial mode to rotate samples.

**Key Integration Points:**
- `chess-scribe-convert/src/components/ImageUpload.tsx` (`handleSampleImage`, ~line 376)
- `chess-scribe-convert/src/utils/sampleImage.ts` (new)

**Deliverables:**
- User lands on `/try` and immediately sees a processed scoresheet.

### Phase 4: Gate write/persistent actions

**Agent should:**
- Identify write/persistent CTAs and wrap them with a `requireSignIn(redirectTarget)` helper that, when `isTrial && !isAuthenticated`, navigates to `/signin` with `state.from = '/'` (or `/try` if we want to drop them back to trial — recommend `/` so they continue with their own data).
- Targets:
  - File picker / drag-drop in `ImageUpload.tsx` (when user picks a custom file in trial mode).
  - **Save to projects / Mark processing complete** in `NotationDisplay.tsx` (`handleMarkProcessingComplete`).
  - **Continuation / dual-page upload** flows.
  - Any “Open in my Lichess account” style export (generic anonymous Lichess board link can remain).
- The gate should show a tiny modal/toast: *“Sign in to upload your own image and save your games”* with a **Sign in** button.

**Deliverables:**
- Trial users can explore but cannot mutate anything that should be tied to an account.

### Phase 5: Sticky upgrade CTA + copy

**Agent should:**
- Add a non-dismissible (or session-dismissible) banner at the top of `/try`: *“You’re trying ChessScribe with a sample game. Sign in to upload your own scoresheets and save your games.”* with a **Sign in** button.
- Update `SignIn.tsx` with a small explanatory line under the title: *“Or [try it first with a sample game](/try)”*.

**Deliverables:**
- Clear path from trial → sign-in → real flow.

### Phase 6: Telemetry hooks (lightweight)

**Agent should:**
- If a telemetry / analytics layer exists (check `src/`), add events:
  - `trial_started`
  - `trial_sample_swapped`
  - `trial_gate_hit` (with `action`)
  - `trial_signed_in` (when user converts from `/try`)
- If no analytics exists, add structured `console.info` markers and log it as a follow-up enhancement.

**Deliverables:**
- Minimum hooks to measure conversion from trial to sign-in.

### Phase 7: Tests + polish

**Agent should:**
- Add Playwright e2e: visit `/try` unauthenticated, verify sample image auto-loads, verify mock endpoint is called (intercept), verify clicking a gated CTA navigates to `/signin` and back.
- `npm run build` and ensure no auth-required hooks crash on `/try`.
- Verify on sign-in we restore `useMockApi` to its previous state (no leaking mock mode into the real account).

**Deliverables:**
- Green build, e2e coverage, screenshots in PR.

## Technical Specifications

### Routing change

```tsx
<Routes>
  <Route path="/signin" element={<SignIn />} />
  <Route path="/try" element={<Trial />} />
  <Route path="/" element={<ProtectedRoute><Index /></ProtectedRoute>} />
  ...
</Routes>
```

### Trial mode hook

```ts
export function useTrialMode() {
  useEffect(() => {
    const prev = localStorage.getItem('chessDecoder_useMockApi');
    localStorage.setItem('chessDecoder_useMockApi', 'true');
    return () => {
      if (prev === null) localStorage.removeItem('chessDecoder_useMockApi');
      else localStorage.setItem('chessDecoder_useMockApi', prev);
    };
  }, []);
}
```

### Gate helper

```tsx
function GatedAction({ when, onAllowed, children, signInMessage }: {
  when: boolean;
  onAllowed: () => void;
  children: (onClick: () => void) => React.ReactNode;
  signInMessage?: string;
}) {
  const navigate = useNavigate();
  const handle = () => when
    ? onAllowed()
    : navigate('/signin', { state: { from: '/', message: signInMessage } });
  return <>{children(handle)}</>;
}
```

### File touch list (expected)

```
chess-scribe-convert/src/App.tsx
chess-scribe-convert/src/pages/Trial.tsx                   (new)
chess-scribe-convert/src/pages/SignIn.tsx
chess-scribe-convert/src/components/Header.tsx
chess-scribe-convert/src/components/ImageUpload.tsx
chess-scribe-convert/src/components/NotationDisplay.tsx
chess-scribe-convert/src/contexts/TrialModeContext.tsx     (new)
chess-scribe-convert/src/hooks/useTrialMode.ts             (new)
chess-scribe-convert/src/utils/sampleImage.ts              (new)
chess-scribe-convert/tests/e2e/trial-mode.spec.ts          (new)
```

## Acceptance Criteria

### Backend

- [ ] No backend change needed — `/api/mock/upload` already exists and returns a deterministic mock response.

### Frontend

- [ ] Visiting `/try` unauthenticated renders the full landing experience without redirecting to `/signin`.
- [ ] On `/try`, a sample scoresheet auto-loads and the mock endpoint (`/api/mock/upload`) is called instead of the real one.
- [ ] User can navigate moves, auto-play, view PGN, and interact with the board on `/try` with no auth.
- [ ] Attempting to upload a custom file, save to projects, mark processing complete, or use continuation flows from `/try` redirects to `/signin` with a clear message.
- [ ] After successful sign-in, the user lands on `/` with mock mode disabled (i.e. real endpoint is used).
- [ ] `SignIn.tsx` shows a visible **“Try without signing in”** entry point.
- [ ] Build passes (`npm run build`); existing tests pass; new `trial-mode.spec.ts` passes.

## Dependencies

### Backend

- Existing `/api/mock/upload` endpoint.

### Frontend

- Existing `useMockApi` runtime override in `imageService.ts`.
- Existing sample image set under `public/sample-images/`.
- Existing `AuthContext`, `ProtectedRoute`, `SignIn.tsx`.

## Impact Assessment

**Impact Level**: High

**Impact Description**: Directly attacks the activation funnel. Lets first-time visitors understand the product before being asked to sign in, which is the single biggest blocker to adoption today. Reuses already-built mock endpoint and sample images, so risk is low.

## Effort Estimation

**Effort Level**: 3 (1–10)

**Effort Breakdown**:

### Backend

- 0 hours.

### Frontend

- Public route + Trial page shell: 2 h
- `useTrialMode` hook + restore-on-exit: 1 h
- Sample image util extraction + auto-load: 2 h
- Gating write actions + sign-in redirect with return state: 3 h
- Sticky CTA banner + SignIn copy: 1 h
- Telemetry hooks + e2e tests: 3 h
- **Frontend Total**: ~12 hours

**Total Estimated**: ~12 hours

## Future Enhancements

- A **guided tour** overlay on `/try` (highlight: image, OCR result, board, PGN) — natural extension of PG-181-style hand-holding.
- Multiple sample scenarios (clean image vs. low-quality) so users can see realistic accuracy ranges.
- Pre-baked “famous game” samples (e.g. *Opera Game*) to make the trial more delightful.
- Server-side trial endpoint with rate limiting if abuse becomes a concern (currently mocked client-side, so no server cost).
- A/B test sign-in conversion: trial → sign-in vs. sign-in → first action.
