---
id: PG-178
status: completed
priority_score: 1.0000
effort: 5
impact: 5
dependencies: []
jira_key: "PG-178"
jira_url: ""
created_date: "2025-12-07"
updated_date: "2025-12-07"
plan_type: agent_plan
executable: false
---

# Implementation Plan: PGN Update and Processing Completion Flag

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Implement functionality to persist edited PGN content to the backend database when users make edits to the chess notation, and add a processing completion flag that is set when users export the game to Lichess or Chess.com. This ensures that user edits are saved and provides a clear indicator that the processing cycle has been completed with at least one acceptable PGN value.

## Plan Overview

The system should:
1. Persist edited PGN content to the backend database when users make edits to moves in the frontend
2. Add a `ProcessingCompleted` flag to the `ChessGame` model to track when a user has completed the processing cycle
3. Set the completion flag when users click "Open in Lichess" or "Open in Chess.com" buttons
4. Provide API endpoints to update PGN content and mark processing as completed
5. Update the frontend to automatically save edits and set the completion flag on export

## Implementation Plan

### Phase 1: Data Model Updates

**Agent should:**
- Review existing `ChessGame` model structure
- Add new field `ProcessingCompleted` (bool, default false) to track completion status
- Add optional field `LastEditedAt` (DateTime, nullable) to track when PGN was last edited
- Add optional field `EditCount` (int, default 0) to track number of edits (optional, for analytics)
- Update database schema/migrations if using SQL
- Ensure backward compatibility (existing games should have `ProcessingCompleted = false`)
- Update Firestore data model annotations if using Firestore

**Deliverables:**
- Updated `ChessGame` model with new fields
- Database migration scripts (if using SQL)
- Data model documentation

### Phase 2: Backend Service Layer - PGN Update Service

**Agent should:**
- Create or extend service interface `IPgnUpdateService` (or extend `GameManagementService`)
- Implement PGN update logic:
  - Validate that game exists and belongs to user
  - Validate PGN content format
  - Update `PgnContent` field in database
  - Update `LastEditedAt` timestamp
  - Increment `EditCount` if tracking edits
  - Return updated game entity
- Implement processing completion logic:
  - Set `ProcessingCompleted` flag to true
  - Update timestamp when completion flag is set
  - Ensure idempotency (can be called multiple times safely)
- Add error handling for invalid games, unauthorized access, and database failures
- Ensure async operations to avoid blocking

**Key Integration Points:**
- Integrate with existing `IChessGameRepository.UpdateAsync` method
- Use existing repository layer for database operations
- Follow existing service patterns and error handling

**Deliverables:**
- `IPgnUpdateService` interface (or extended interface)
- PGN update service implementation
- Processing completion service methods
- Error handling and validation logic

### Phase 3: API Endpoints

**Agent should create/update:**
- Create `PUT /api/game/{gameId}/pgn` endpoint:
  - Accept `UpdatePgnRequest` DTO with:
    - `pgnContent` (string, required) - the updated PGN content
  - Validate game exists and belongs to authenticated user
  - Update PGN content in database
  - Return updated game response
  - Handle errors (404 if game not found, 403 if unauthorized, 400 if invalid PGN)
- Create `PUT /api/game/{gameId}/complete` endpoint:
  - Mark processing as completed
  - Set `ProcessingCompleted` flag to true
  - Return updated game response
  - Idempotent (can be called multiple times)
- Update `GET /api/game/{gameId}` endpoint:
  - Return `ProcessingCompleted` flag in response
  - Return `LastEditedAt` timestamp if available
- Create request DTOs:
  - `UpdatePgnRequest` DTO
  - `CompleteProcessingRequest` DTO (optional, can be empty body)

**Deliverables:**
- New API controller endpoints
- Request/response DTOs
- API validation logic
- Error handling and HTTP status codes
- API documentation updates

### Phase 4: Backend Testing

**Agent should create:**
- Unit tests for PGN update service:
  - Test successful PGN update
  - Test validation of PGN format
  - Test unauthorized access handling
  - Test game not found scenarios
- Unit tests for processing completion:
  - Test setting completion flag
  - Test idempotency (multiple calls)
  - Test authorization checks
- Integration tests for API endpoints:
  - Test full update flow
  - Test completion flag setting
  - Test error scenarios
- Edge case tests:
  - Very long PGN content
  - Invalid PGN format
  - Concurrent updates
  - Missing game ID

**Deliverables:**
- Comprehensive test suite
- Test coverage for all scenarios
- Edge case validation

### Phase 5: Frontend Implementation

**Agent should:**

#### 5.1 Update Image Service
- Extend `imageService.ts`:
  - Add `updateGamePgn(gameId: string, pgnContent: string): Promise<ChessGame>`
  - Add `markProcessingComplete(gameId: string): Promise<ChessGame>`
- Add TypeScript interfaces:
  - `UpdatePgnRequest` (pgnContent: string)
  - Extended `ChessGame` interface with `processingCompleted?: boolean` and `lastEditedAt?: string`
- Integrate with existing API configuration in `src/config/api.ts`

#### 5.2 Update NotationDisplay Component
- Modify `NotationDisplay.tsx`:
  - Add debounced auto-save functionality for PGN edits:
    - Save edited PGN to backend when user stops editing (debounce ~2-3 seconds)
    - Show loading indicator during save
    - Show success/error toast notifications
  - Update `handleOpenInLichess` function:
    - Call `markProcessingComplete` API before opening Lichess
    - Handle completion flag setting
    - Show completion confirmation if needed
  - Update `handleOpenInChessCom` function:
    - Call `markProcessingComplete` API before opening Chess.com
    - Handle completion flag setting
    - Show completion confirmation if needed
  - Add visual indicator if processing is completed:
    - Show badge or icon when `processingCompleted` is true
    - Display "Processing Complete" status
  - Store `gameId` in component state (passed from parent or extracted from data)

#### 5.3 Add Auto-Save Logic
- Implement debounced save mechanism:
  - Track when PGN content changes
  - Debounce save operation (wait 2-3 seconds after last edit)
  - Cancel pending saves if new edits occur
  - Show subtle save indicator (e.g., "Saving..." or checkmark icon)
- Handle save errors gracefully:
  - Show error toast if save fails
  - Retry logic (optional)
  - Allow manual retry if auto-save fails

#### 5.4 Update Main Page Flow
- Modify `Index.tsx`:
  - Pass `gameId` to `NotationDisplay` component
  - Handle completion status updates
  - Update UI to reflect completion status
  - Store completion status in state

#### 5.5 Add Completion Status Display
- Create completion status indicator:
  - Badge or icon showing "Processing Complete"
  - Display completion timestamp if available
  - Visual feedback when completion flag is set
- **GTM Analytics Integration** (for completion rate tracking):
  - Track event when completion flag is set (on export to Lichess/Chess.com)
  - Track event when PGN is auto-saved
  - Track completion timestamp for analytics
  - Enable backend to query completion rate for GTM metrics

**Key Integration Points:**
- Integrate with existing `NotationDisplay` component
- Use existing authentication context
- Follow existing UI patterns and design system
- Maintain consistency with current component structure

**Deliverables:**
- Updated `imageService.ts` with PGN update and completion methods
- Updated `NotationDisplay.tsx` with auto-save and completion logic
- Updated `Index.tsx` with game ID passing
- TypeScript type definitions
- Auto-save debouncing logic
- Completion status UI components

## Technical Specifications

### Data Model Extensions
```csharp
// Extend ChessGame model
public class ChessGame
{
    // ... existing fields ...
    
    [FirestoreProperty]
    public bool ProcessingCompleted { get; set; } = false;
    
    [FirestoreProperty]
    public DateTime? LastEditedAt { get; set; }
    
    [FirestoreProperty]
    public int EditCount { get; set; } = 0; // Optional, for analytics
}
```

### Service Interface (Proposed)
```csharp
public interface IPgnUpdateService
{
    Task<ChessGame> UpdatePgnContentAsync(Guid gameId, string userId, string pgnContent);
    Task<ChessGame> MarkProcessingCompleteAsync(Guid gameId, string userId);
    Task<ChessGame?> GetGameAsync(Guid gameId, string userId);
}
```

### API Endpoints
```typescript
// Backend API endpoints to implement:
PUT /api/game/{gameId}/pgn
  Request: { pgnContent: string }
  Response: ChessGame

PUT /api/game/{gameId}/complete
  Request: {} (empty body)
  Response: ChessGame

GET /api/game/{gameId}
  Response: ChessGame (includes processingCompleted, lastEditedAt)
```

### Frontend TypeScript Interfaces
```typescript
// src/services/imageService.ts
export interface UpdatePgnRequest {
  pgnContent: string;
}

export interface ChessGame {
  // ... existing fields ...
  processingCompleted?: boolean;
  lastEditedAt?: string;
  editCount?: number;
}

export const updateGamePgn = async (
  gameId: string,
  pgnContent: string
): Promise<ChessGame> => {
  // Implementation
};

export const markProcessingComplete = async (
  gameId: string
): Promise<ChessGame> => {
  // Implementation
};
```

### Auto-Save Implementation
```typescript
// In NotationDisplay.tsx
const [isSaving, setIsSaving] = useState(false);
const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
const saveTimeoutRef = useRef<NodeJS.Timeout | null>(null);

const handlePgnEdit = (newPgn: string) => {
  // Update local state
  setEditedNotation(newPgn);
  
  // Clear existing timeout
  if (saveTimeoutRef.current) {
    clearTimeout(saveTimeoutRef.current);
  }
  
  // Set saving status
  setSaveStatus('saving');
  
  // Debounce save (2-3 seconds)
  saveTimeoutRef.current = setTimeout(async () => {
    try {
      await updateGamePgn(gameId, newPgn);
      setSaveStatus('saved');
      setTimeout(() => setSaveStatus('idle'), 2000);
    } catch (error) {
      setSaveStatus('error');
      toast({ title: "Save failed", description: "Could not save changes" });
    }
  }, 2500);
};
```

### Completion Flag Logic
```typescript
// In NotationDisplay.tsx
const handleOpenInLichess = async () => {
  try {
    // Mark as complete before opening
    if (gameId && !data.processingCompleted) {
      await markProcessingComplete(gameId);
      toast({
        title: "Processing Complete",
        description: "Game marked as complete",
      });
    }
    
    // Existing Lichess opening logic...
  } catch (error) {
    // Handle error
  }
};
```

### Frontend Component Structure
```
src/
  components/
    NotationDisplay.tsx        // Updated with auto-save and completion
  services/
    imageService.ts            // Extended with update methods
  pages/
    Index.tsx                   // Updated with game ID passing
```

### Frontend UI Requirements
- Auto-save indicator:
  - Subtle "Saving..." text or spinner when saving
  - Checkmark icon when saved
  - Error icon if save fails
- Completion status badge:
  - "Processing Complete" badge when flag is set
  - Optional timestamp display
  - Visual distinction (e.g., green badge)
- Use existing design system:
  - shadcn/ui components (Badge, Toast, etc.)
  - Tailwind CSS for styling
  - Responsive design for mobile/tablet/desktop
  - Consistent with existing `NotationDisplay.tsx` patterns

## Acceptance Criteria

### Backend
- [ ] `ChessGame` model extended with `ProcessingCompleted` field
- [ ] `ChessGame` model extended with `LastEditedAt` field (optional)
- [ ] Database schema updated (migration if needed)
- [ ] `PUT /api/game/{gameId}/pgn` endpoint created and functional
- [ ] `PUT /api/game/{gameId}/complete` endpoint created and functional
- [ ] PGN content validation implemented
- [ ] Authorization checks (user owns game)
- [ ] Error handling for invalid games, unauthorized access
- [ ] Backward compatibility maintained (existing games work)
- [ ] Unit tests for PGN update service
- [ ] Unit tests for completion flag service
- [ ] Integration tests for API endpoints

### Frontend
- [ ] `imageService.ts` extended with `updateGamePgn` method
- [ ] `imageService.ts` extended with `markProcessingComplete` method
- [ ] `NotationDisplay.tsx` updated with auto-save functionality
- [ ] Auto-save debouncing implemented (2-3 second delay)
- [ ] Save status indicators (saving/saved/error) displayed
- [ ] `handleOpenInLichess` updated to set completion flag
- [ ] `handleOpenInChessCom` updated to set completion flag
- [ ] Completion status badge/indicator displayed
- [ ] Game ID passed to `NotationDisplay` component
- [ ] Error handling for save failures
- [ ] Toast notifications for save status
- [ ] Responsive design for all screen sizes
- [ ] UI follows existing design system

## Dependencies

### Backend
- Existing `ChessGame` model
- Existing `IChessGameRepository` interface
- Existing repository implementations (SQLite/Firestore)
- API controller infrastructure
- Authentication/authorization middleware

### Frontend
- Existing `NotationDisplay` component
- Existing `imageService` API client
- Existing authentication context
- UI component library (shadcn/ui)
- Toast notification system
- Tailwind CSS for styling

## Impact Assessment

**Impact Level**: Medium

**Impact Description**: 
This feature improves data persistence and user experience by ensuring that user edits to PGN content are saved to the database, preventing data loss. The processing completion flag provides a clear indicator that the user has completed the processing cycle with at least one acceptable PGN value, which can be useful for analytics, user progress tracking, and future features like game history or export workflows. This feature enhances the reliability of the system and provides better user feedback about the state of their processed games.

**GTM Alignment**:
- **Critical KPI Tracking**: Enables tracking of "Processing Completion Rate" - a key Phase 1 metric (target: ≥40%)
- **Activation Measurement**: Completion flag indicates users successfully completed full workflow (upload → edit → export)
- **Retention Indicator**: Users who complete games are more likely to return
- **Phase 2 Readiness**: Completion rate is a trigger metric for moving to monetization phase
- **Key Metrics to Track**:
  - Processing completion rate: % of users who export at least one game (target: ≥40%)
  - Average time to completion (from upload to export)
  - Edit frequency (how many edits before completion)
  - Completion rate by user segment (first-time vs. returning users)

## Effort Estimation

**Effort Level**: 5

**Effort Breakdown**:

### Backend
- Data model and schema updates: 1 hour
- PGN update service implementation: 2 hours
- Processing completion service: 1 hour
- API endpoints implementation: 2 hours
- Validation and error handling: 1.5 hours
- Unit tests: 2 hours
- Integration tests: 1.5 hours
- **Backend Total**: 11 hours

### Frontend
- TypeScript interfaces and service updates: 1 hour
- Auto-save debouncing logic: 2 hours
- Update `NotationDisplay.tsx` with auto-save: 2.5 hours
- Update completion flag logic in export buttons: 1.5 hours
- Completion status UI components: 1.5 hours
- Update `Index.tsx` with game ID passing: 1 hour
- Error handling and toast notifications: 1.5 hours
- UI styling and responsive design: 1.5 hours
- Testing and refinement: 2 hours
- **Frontend Total**: 14.5 hours

**Total Estimated**: 25.5 hours

## Future Enhancements

This feature establishes the foundation for persistent editing and completion tracking. Future enhancements may include:
- Edit history/version tracking (undo/redo functionality)
- Real-time collaboration (multiple users editing same game)
- Conflict resolution for concurrent edits
- Export completion analytics
- Batch completion operations
- Completion notifications
- Edit diff visualization
- Auto-save configuration (user preferences for save frequency)

