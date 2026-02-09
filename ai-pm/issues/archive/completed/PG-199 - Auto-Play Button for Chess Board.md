---
id: PG-199
status: completed
priority_score: 1.6666
effort: 3
impact: 5
dependencies: []
created_date: "2026-01-10"
updated_date: "2026-02-09"
plan_type: agent_plan
executable: false
---

# Implementation Plan: Auto-Play Button for Chess Board

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Replace the reset button in the ChessBoard component with an auto-play button that automatically plays through all game moves sequentially with a 2-second delay between each move. This allows users to watch the game replay automatically without manually clicking through each move.

## Plan Overview

The system should:
1. Remove the existing reset button from the ChessBoard component controls
2. Add an auto-play button that toggles between play and pause states
3. Implement auto-play logic that advances through moves with a 2-second delay
4. Handle edge cases: stopping at game end, pausing/resuming, and cleanup on unmount
5. Update the ChessBoardRef interface to expose auto-play control methods (optional)

## Implementation Plan

### Phase 1: Remove Reset Button and Add Auto-Play Button

**Agent should:**
- Remove the reset button from the ChessBoard component (lines 449-456 in `chess-scribe-convert/src/components/ChessBoard.tsx`)
- Import `Play` and `Pause` icons from `lucide-react`
- Add state to track auto-play status (`isAutoPlaying`)
- Add state to track the auto-play interval/timeout reference (`autoPlayTimeoutRef`)
- Replace the reset button with an auto-play button that shows Play icon when stopped and Pause icon when playing

**Key Integration Points:**
- Existing button controls section (lines 428-457)
- Icon imports from lucide-react (line 5)
- State management for game position tracking

**Deliverables:**
- Removed reset button
- New auto-play button with Play/Pause icons
- State variables for auto-play control

### Phase 2: Implement Auto-Play Logic

**Agent should:**
- Create `startAutoPlay()` function that:
  - Resets to the beginning of the game if at the end
  - Sets up an interval/timeout to call `goForward()` every 2 seconds
  - Stops automatically when reaching the last move
  - Updates button state to show Pause icon
- Create `stopAutoPlay()` function that:
  - Clears any active intervals/timeouts
  - Updates button state to show Play icon
- Handle edge cases:
  - If already at the last move, reset to beginning before starting
  - If user manually navigates during auto-play, stop auto-play
  - Clean up intervals on component unmount

**Key Integration Points:**
- Existing `goForward()` function (lines 397-401)
- Existing `currentMoveIndex` state
- Existing `gameHistory` state
- `useEffect` cleanup for unmount

**Deliverables:**
- `startAutoPlay()` function
- `stopAutoPlay()` function
- Auto-play state management
- Cleanup logic

### Phase 3: Update Component Interface and User Experience

**Agent should:**
- Update the auto-play button to:
  - Show Play icon when `isAutoPlaying` is false
  - Show Pause icon when `isAutoPlaying` is true
  - Be disabled when there are no moves to play
  - Toggle between start/stop when clicked
- Add visual feedback (optional): consider adding a subtle animation or indicator when auto-play is active
- Ensure manual navigation (backward/forward buttons) stops auto-play if it's running
- Update button tooltips to indicate auto-play functionality

**Key Integration Points:**
- Button component styling (lines 429-456)
- Icon rendering logic
- Disabled state logic

**Deliverables:**
- Updated button UI with proper icons
- Toggle functionality
- Disabled state handling
- User interaction handling

### Phase 4: Update ChessBoardRef Interface (Optional Enhancement)

**Agent should:**
- Consider adding `startAutoPlay()` and `stopAutoPlay()` methods to the `ChessBoardRef` interface (lines 20-25)
- This allows parent components to control auto-play programmatically if needed in the future
- Update `useImperativeHandle` to expose these methods (lines 44-49)

**Key Integration Points:**
- `ChessBoardRef` interface
- `useImperativeHandle` hook

**Deliverables:**
- Updated interface (optional, can be deferred)
- Exposed methods in ref (optional)

## Technical Specifications

### State Management
```typescript
// Add to ChessBoard component state
const [isAutoPlaying, setIsAutoPlaying] = useState(false);
const autoPlayTimeoutRef = useRef<NodeJS.Timeout | null>(null);
```

### Auto-Play Functions
```typescript
const startAutoPlay = () => {
  // If at the end, reset to beginning
  if (currentMoveIndex >= gameHistory.length - 1) {
    goToMove(-1); // Reset to start
  }
  
  setIsAutoPlaying(true);
  
  const playNextMove = () => {
    if (currentMoveIndex < gameHistory.length - 1) {
      goForward();
      autoPlayTimeoutRef.current = setTimeout(playNextMove, 2000);
    } else {
      // Reached the end
      stopAutoPlay();
    }
  };
  
  // Start the first move after 2 seconds
  autoPlayTimeoutRef.current = setTimeout(playNextMove, 2000);
};

const stopAutoPlay = () => {
  if (autoPlayTimeoutRef.current) {
    clearTimeout(autoPlayTimeoutRef.current);
    autoPlayTimeoutRef.current = null;
  }
  setIsAutoPlaying(false);
};
```

### Button Implementation
```typescript
<Button
  onClick={() => isAutoPlaying ? stopAutoPlay() : startAutoPlay()}
  disabled={gameHistory.length === 0}
  variant="outline"
  size="sm"
  className="flex items-center gap-1 px-3 py-2"
  title={isAutoPlaying ? "Pause auto-play" : "Start auto-play"}
>
  {isAutoPlaying ? <Pause size={16} /> : <Play size={16} />}
  <span className="text-sm">{isAutoPlaying ? "Pause" : "Auto Play"}</span>
</Button>
```

### Cleanup Effect
```typescript
useEffect(() => {
  return () => {
    // Cleanup on unmount
    if (autoPlayTimeoutRef.current) {
      clearTimeout(autoPlayTimeoutRef.current);
    }
  };
}, []);
```

### Frontend Component Structure
```
chess-scribe-convert/src/components/
  ChessBoard.tsx (modify existing)
```

### Frontend UI Requirements
- Use existing shadcn/ui Button component
- Use lucide-react icons (Play, Pause)
- Maintain consistent styling with existing controls
- Responsive design for mobile/tablet/desktop
- Accessible button labels and tooltips

## Acceptance Criteria

### Frontend
- [ ] Reset button is completely removed from ChessBoard component
- [ ] Auto-play button appears in place of reset button
- [ ] Auto-play button shows Play icon when stopped
- [ ] Auto-play button shows Pause icon when playing
- [ ] Clicking auto-play button starts playback from current position (or beginning if at end)
- [ ] Auto-play advances through moves with exactly 2 seconds between each move
- [ ] Auto-play automatically stops when reaching the last move
- [ ] Clicking pause button stops auto-play at current position
- [ ] Manual navigation (backward/forward buttons) stops auto-play if active
- [ ] Auto-play button is disabled when there are no moves to play
- [ ] No memory leaks: intervals/timeouts are properly cleaned up on unmount
- [ ] Auto-play works correctly even if user manually navigates during playback

## Dependencies

### Frontend
- Existing `ChessBoard` component (`chess-scribe-convert/src/components/ChessBoard.tsx`)
- `lucide-react` library (already installed)
- `react-chessboard` library (already installed)
- `chess.js` library (already installed)
- Existing `goForward()` and `goToMove()` functions
- Existing state management for `currentMoveIndex` and `gameHistory`

## Impact Assessment

**Impact Level**: Medium

**Impact Description**: 
This feature improves the user experience by allowing automatic game replay, making it easier to review games without manual navigation. It's a quality-of-life improvement that enhances the chess board viewing experience, particularly useful for analyzing longer games.

## Effort Estimation

**Effort Level**: 3

**Effort Breakdown**:

### Frontend
- Remove reset button and add auto-play button UI: 1 hour
- Implement auto-play logic with timing: 2 hours
- Handle edge cases and cleanup: 1 hour
- Testing and refinement: 1 hour
- **Frontend Total**: 5 hours

**Total Estimated**: 5 hours

## Future Enhancements

This feature establishes the foundation for automatic game replay. Future enhancements may include:
- Configurable playback speed (1x, 2x, 0.5x)
- Loop playback option
- Skip to specific move during auto-play
- Keyboard shortcuts for play/pause
- Visual progress indicator showing current move position
