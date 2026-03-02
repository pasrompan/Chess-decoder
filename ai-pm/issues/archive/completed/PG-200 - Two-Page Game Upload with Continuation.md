---
id: PG-200
status: completed
priority_score: 1.6000
effort: 5
impact: 8
dependencies: []
created_date: "2026-02-28"
updated_date: 2026-02-28
plan_type: agent_plan
executable: false
---

# Implementation Plan: Two-Page Game Upload with Continuation

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Implement functionality to allow users to upload two images that together form a complete chess game spanning two scoresheet pages. When a game is long (e.g., 60+ moves), the notation often spans two physical pages. Users should be able to upload both pages together (or add a second page later) and have the moves automatically merged to create a complete, unified PGN.

## Plan Overview

The system should support two upload flows:

**Flow 1: Direct Dual-Image Upload (Primary)**
- User uploads two images simultaneously from the start
- Both images are processed in parallel
- System detects which is page 1 vs page 2 based on move numbers
- Moves are merged into a single unified PGN

**Flow 2: Add Continuation to Existing Game**
- User has already processed page 1
- User uploads page 2 as a continuation
- System appends page 2 moves to existing game

The system should:
1. Allow users to upload two images at once for a complete game
2. Allow users to add a continuation image to an existing game
3. Automatically detect page order based on move numbers
4. Validate continuity between pages (page 1 ends where page 2 begins)
5. Merge moves sequentially to create a complete game record

## Implementation Plan

### Phase 1: Data Model and Schema Design

**Agent should:**
- Review existing `GameImage` and `ChessGame` models
- Design schema for tracking two pages per game:
  - Add `pageNumber` field to images (1 or 2)
  - Add `continuationImageId` to link page 1 to page 2
  - Store page-level move ranges (e.g., page 1: moves 1-60, page 2: moves 61-85)
- Design continuation metadata structure:
  - Starting move number for each page
  - Ending move number for each page
  - Continuation validation status
- Keep schema simple for exactly 2 pages (no need for complex linked lists)

**Deliverables:**
- Updated data model specification
- Database schema changes (if needed)
- Two-page relationship design

### Phase 2: Backend Service Layer - Dual Image Processing

**Agent should:**
- Create or extend service interface for two-page handling
- Implement dual image upload endpoint:
  - Accept two image files simultaneously
  - Process both images in parallel using existing `ImageExtractionService`
  - Detect move numbers from each image to determine page order
  - Automatically assign page 1 vs page 2 based on starting move numbers
  - Merge moves into unified PGN
- Implement continuation image upload endpoint (for adding page 2 later):
  - Accept game ID and continuation image file
  - Validate that game exists, belongs to user, and doesn't already have page 2
  - Detect starting move number from continuation image
  - Process continuation image using existing `ImageExtractionService`
  - Store continuation image linked to primary game
- Implement move merging logic:
  - Validate that page 2 starts where page 1 ended (or close to it)
  - Handle overlap (deduplicate if pages share some moves)
  - Append page 2 moves to page 1 PGN
  - Update total move count and game metadata
- Implement continuation validation:
  - Check that no move numbers are duplicated
  - Check for gaps between pages (warn user)
  - Validate legal chess positions across page boundary
  - Flag potential issues for user review

**Key Integration Points:**
- Extend `GameProcessingService` or create new service
- Use existing `ImageExtractionService.ProcessImageAsync`
- Integrate with existing PGN generation and validation logic
- Update game record with merged moves

**Deliverables:**
- Dual image processing service implementation
- Continuation processing service implementation
- Move merging logic
- Continuation validation logic
- Updated game processing flow

### Phase 3: Move Merging Algorithm

**Agent should implement:**
- Starting move detection:
  - Parse each image to find first move number
  - Handle different notation styles (61., 61..., etc.)
  - Detect if image starts with White or Black move
- Page order detection (for dual upload):
  - Compare starting move numbers from both images
  - Automatically assign lower number as page 1, higher as page 2
  - Handle edge case where both start with move 1 (error)
- Move range validation:
  - Verify page 2 starts where page 1 ended (or within 1-2 moves for overlap)
  - Handle cases where pages overlap (deduplicate redundant moves)
  - Handle cases where pages have gaps (warn user, still merge)
- Sequential merging:
  - Append page 2 moves to page 1 PGN string
  - Maintain proper PGN formatting
  - Preserve all metadata and headers from page 1
  - Update result if game concludes on page 2
- Edge case handling:
  - Page 2 starts mid-move (Black's response to move 60)
  - Game ends on page 2 (checkmate, resignation, draw)
  - Pages have slight overlap (1-3 moves repeated)

**Deliverables:**
- Starting move detection algorithm
- Page order detection logic
- Move range validation logic
- Sequential merging implementation
- Edge case handling

### Phase 4: API Endpoints

**Agent should create:**
- `POST /api/game/upload-dual` endpoint:
  - Accept multipart form data with two image files (page1, page2)
  - Process both images in parallel
  - Detect page order automatically
  - Merge into single game with unified PGN
  - Return complete game with both pages info
- `POST /api/game/{gameId}/continuation` endpoint:
  - Accept multipart form data with continuation image file
  - Validate game doesn't already have a page 2
  - Process continuation image
  - Append to existing game
  - Return updated game with all moves
- `GET /api/game/{gameId}/pages` endpoint:
  - Return both page images associated with a game (if two-page game)
  - Include page metadata and move ranges
- `DELETE /api/game/{gameId}/continuation` endpoint:
  - Remove page 2 from a game
  - Recalculate game PGN from page 1 only
- Update existing game endpoints to include `hasContinuation` flag and page info

**Deliverables:**
- New API controller endpoints
- Request/response DTOs for dual upload and continuation operations
- Error handling and validation
- API documentation updates

### Phase 5: Backend Testing

**Agent should create:**
- Unit tests for starting move detection
- Unit tests for page order detection
- Unit tests for move merging logic
- Unit tests for continuation validation
- Integration tests for dual image upload flow
- Integration tests for continuation upload flow
- Test cases for edge cases:
  - Overlapping pages (1-3 moves repeated)
  - Gap between pages (missing moves)
  - Game ending on page 2
  - Page 2 starting with Black move
  - Both images having same starting move (error case)

**Deliverables:**
- Comprehensive test suite
- Test data with sample two-page game images
- Integration test coverage

### Phase 6: Frontend Implementation

**Agent should:**

#### 6.1 Update Image Service
- Extend `imageService.ts`:
  - Add `uploadDualImages(page1: File, page2: File): Promise<DualUploadResult>`
  - Add `uploadContinuation(gameId: string, imageFile: File): Promise<ContinuationResult>`
  - Add `getGamePages(gameId: string): Promise<GamePageInfo[]>`
  - Add `deleteContinuation(gameId: string): Promise<void>`
- Add TypeScript interfaces:
  - `DualUploadResult` (complete game with merged PGN, both pages info)
  - `ContinuationResult` (updated PGN, page 2 info, validation status)
  - `GamePageInfo` (page metadata, move range, image reference)

#### 6.2 Update ImageUpload Component for Dual Upload
- Extend existing `ImageUpload.tsx` component:
  - Add toggle/option for "Game spans two pages"
  - When enabled, show two upload areas: "Page 1" and "Page 2"
  - Allow drag-and-drop for both images
  - Process both images when user clicks submit
  - Show combined processing status
- Keep existing single-image flow as default
- Use existing UI components (Card, Button, Progress, etc.)

#### 6.3 Create Continuation Upload Component
- Create `ContinuationUpload.tsx` component:
  - Display current game information and move count
  - Show "Last move" indicator (e.g., "Current game ends at move 60")
  - Display upload area for page 2 image
  - Show processing status and validation results
  - Display appended moves after processing
  - Show success/warning/error states
- Integrate with existing `ImageUpload` component patterns

#### 6.4 Update NotationDisplay Component
- Extend `NotationDisplay.tsx`:
  - Add button/link to upload continuation page (if no page 2 exists)
  - Show indicator for two-page games (e.g., "2 pages" badge)
  - Display page break indicator in move list (subtle separator at page boundary)
  - Show "Add page 2" prompt after last move for single-page games
- Update to handle merged PGN content
- Show page info (e.g., "Page 1: moves 1-60 | Page 2: moves 61-85")

#### 6.5 Update Main Page Flow
- Modify `Index.tsx`:
  - Handle dual image upload flow
  - Handle continuation upload flow
  - Update state management for two-page games
- Add clear UX for "game continues on another page" scenario
- Show page summary when game has two pages

#### 6.6 UI/UX Enhancements
- Add visual feedback:
  - Loading states during dual/continuation processing
  - Success/warning/error messages
  - Progress indicators for both pages
  - "2 pages" badge for two-page games
- Add helpful prompts:
  - "Game spans two pages?" toggle on upload
  - "Add second page" button after processing single page
  - Tooltip explaining the feature
- Responsive design for mobile/tablet/desktop
- Accessibility considerations

**Key Integration Points:**
- Integrate with existing `ImageUpload` and `NotationDisplay` components
- Use existing authentication context
- Follow existing UI patterns and design system
- Maintain consistency with current component structure

**Deliverables:**
- Updated `ImageUpload.tsx` with dual upload option
- `ContinuationUpload.tsx` component
- Updated `imageService.ts` with dual and continuation methods
- Updated `NotationDisplay.tsx` with page 2 support
- Updated `Index.tsx` with two-page flow
- TypeScript type definitions
- UI styling and responsive design

## Technical Specifications

### Data Model Extensions
```csharp
// Extend GameImage model for two-page tracking
public class GameImage
{
    // Existing fields...
    public int PageNumber { get; set; } = 1;  // 1 or 2
    public int StartingMoveNumber { get; set; }
    public int EndingMoveNumber { get; set; }
    public Guid? ContinuationImageId { get; set; }  // Link from page 1 to page 2
}

// Page information for API responses
public class GamePageInfo
{
    public Guid ImageId { get; set; }
    public int PageNumber { get; set; }  // 1 or 2
    public int StartingMoveNumber { get; set; }
    public int EndingMoveNumber { get; set; }
    public DateTime UploadedAt { get; set; }
}
```

### API Endpoints
```typescript
// Backend API endpoints

// Dual image upload - process two pages at once
POST /api/game/upload-dual
  Body: FormData { page1: File, page2: File }
  Response: {
    gameId: string;
    mergedPgn: string;
    totalMoves: number;
    page1: GamePageInfo;
    page2: GamePageInfo;
    validation: ContinuationValidation;
  }

// Add continuation to existing game
POST /api/game/{gameId}/continuation
  Body: FormData { image: File }
  Response: {
    gameId: string;
    updatedPgn: string;
    totalMoves: number;
    page2: GamePageInfo;
    validation: ContinuationValidation;
  }

// Get pages for a game
GET /api/game/{gameId}/pages
  Response: {
    page1: GamePageInfo;
    page2?: GamePageInfo;  // Optional, only if continuation exists
  }

// Remove continuation (page 2) from a game
DELETE /api/game/{gameId}/continuation
  Response: {
    gameId: string;
    updatedPgn: string;  // Reverted to page 1 only
    totalMoves: number;
  }
```

### Frontend TypeScript Interfaces
```typescript
// src/services/imageService.ts

// Result from uploading two images at once
export interface DualUploadResult {
  gameId: string;
  mergedPgn: string;
  validation: ValidationResult;
  totalMoves: number;
  page1: GamePageInfo;
  page2: GamePageInfo;
  continuationValidation: ContinuationValidation;
}

// Result from adding continuation to existing game
export interface ContinuationResult {
  gameId: string;
  updatedPgn: string;
  validation: ValidationResult;
  totalMoves: number;
  page2: GamePageInfo;
  continuationValidation: ContinuationValidation;
}

export interface GamePageInfo {
  imageId: string;
  pageNumber: 1 | 2;
  startingMoveNumber: number;
  endingMoveNumber: number;
  uploadedAt: string;
}

export interface ContinuationValidation {
  isValid: boolean;
  page1EndMove: number;
  page2StartMove: number;
  hasGap: boolean;
  gapSize?: number;
  hasOverlap: boolean;
  overlapMoves?: number;
  warnings: string[];
}
```

### Move Merging Algorithm Logic
```
For dual image upload:
  1. Process both images in parallel to extract moves
  2. Detect first move number from each image
  3. Assign page order: lower starting move = page 1, higher = page 2
  4. If both start with move 1: return error (not a continuation scenario)
  5. Validate continuity and merge (see below)

For continuation processing (adding page 2 to existing game):
  1. Process continuation image to extract moves
  2. Detect first move number in page 2
  3. Get last move number from page 1
  4. Validate continuity:
     - If page2Start == page1End + 1: perfect continuation
     - If page2Start > page1End + 1: gap detected (warn user, still merge)
     - If page2Start <= page1End: overlap detected (deduplicate)
  5. Handle overlap:
     - Remove duplicate moves from page 2
     - Keep page 1 version of overlapping moves
  6. Append non-duplicate page 2 moves to page 1 PGN
  7. Update game metadata (total moves, hasContinuation = true)
  8. Return merged game with validation info
```

### Frontend Component Structure
```
src/
  components/
    ImageUpload.tsx             // Extended with dual upload option
    ContinuationUpload.tsx      // Add page 2 to existing game
  services/
    imageService.ts             // Extended with dual and continuation methods
  pages/
    Index.tsx                   // Updated with two-page flow
```

## Acceptance Criteria

### Backend
- [ ] Data model supports exactly two pages per game (page 1 + optional page 2)
- [ ] Dual image upload endpoint processes both images and merges them
- [ ] Page order is automatically detected based on starting move numbers
- [ ] Continuation image can be uploaded and associated with existing single-page game
- [ ] Continuation image is processed using existing image extraction service
- [ ] Starting move number is correctly detected from each image
- [ ] Continuation validation identifies gaps and overlaps
- [ ] Move merging algorithm generates correct unified PGN
- [ ] API endpoints for dual upload, continuation upload, and continuation deletion
- [ ] Error handling for invalid games, unauthorized access, processing failures
- [ ] Unit tests for page order detection and merging logic
- [ ] Integration tests for dual upload and continuation flows

### Frontend
- [ ] `ImageUpload.tsx` extended with "Game spans two pages" toggle
- [ ] Dual upload shows two upload areas when toggle is enabled
- [ ] `ContinuationUpload.tsx` component created for adding page 2 later
- [ ] `imageService.ts` extended with dual and continuation methods
- [ ] `NotationDisplay.tsx` updated with "add page 2" option and "2 pages" indicator
- [ ] Two-page upload integrated into main flow
- [ ] Clear indication when game has two pages
- [ ] Validation warnings shown for gaps/overlaps
- [ ] Loading states and error handling
- [ ] Responsive design for all screen sizes
- [ ] UI follows existing design system

## Dependencies

### Backend
- Existing `GameProcessingService`
- Existing `ImageExtractionService`
- Existing `GameImage` repository
- Existing PGN generation logic
- API controller infrastructure

### Frontend
- Existing `ImageUpload` component
- Existing `NotationDisplay` component
- Existing `imageService` API client
- Authentication context
- UI component library (shadcn/ui)
- Tailwind CSS for styling

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
This feature addresses a common real-world scenario where chess games span multiple scoresheet pages. Long tournament games frequently exceed 60 moves, requiring multiple pages of notation. Without this feature, users would need to manually combine PGNs or process only part of their game. This significantly improves usability for serious chess players who often play long games and need complete game records.

## Effort Estimation

**Effort Level**: 5

**Effort Breakdown**:

### Backend
- Data model and schema design: 1 hour
- Dual image processing service: 3 hours
- Continuation processing service: 2 hours
- Page order detection and move merging logic: 3 hours
- API endpoints implementation: 2 hours
- Error handling and edge cases: 2 hours
- Unit tests: 2 hours
- Integration tests: 2 hours
- **Backend Total**: 17 hours

### Frontend
- Dual and continuation service methods: 2 hours
- Update `ImageUpload.tsx` with dual upload toggle: 3 hours
- `ContinuationUpload.tsx` component: 2 hours
- Update `NotationDisplay.tsx`: 2 hours
- Update `Index.tsx` flow: 2 hours
- UI styling and indicators: 2 hours
- Error handling and loading states: 1 hour
- Testing and refinement: 2 hours
- **Frontend Total**: 16 hours

**Total Estimated**: 33 hours

## Relationship to PG-176

This feature (PG-200) is **distinct from but complementary to** PG-176 (Second Image Upload for Move Clarification):

- **PG-176**: Upload a second image from the **second player's perspective** to clarify ambiguous moves. Both images contain the same moves, viewed from different angles/handwriting.
- **PG-200**: Upload a **continuation image** that contains **additional moves** continuing from where the first page ended. Sequential pages of the same scoresheet.

Both features can coexist and may share some infrastructure (multi-image handling), but serve different user needs.

## Future Enhancements

This feature establishes the foundation for two-page game processing. Future enhancements may include:
- Support for combining PG-176 and PG-200 (continuation + clarification from second player)
- Visual page thumbnails showing which moves came from which page
- Support for 3+ pages (rare edge case, not in initial scope)
- Page-level re-processing if OCR quality was poor on one page
