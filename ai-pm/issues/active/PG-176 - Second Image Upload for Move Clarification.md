---
id: PG-176
status: active
priority_score: .6428
effort: 7
impact: 8
dependencies: ["ai-pm/issues/active/PG-156 - Project and History File System for Image Uploads.md"]
jira_key: "PG-176"
jira_url: "https://paschalis-rompanos.atlassian.net/browse/PG-176"
created_date: "2025-11-29"
updated_date: 2026-02-09
plan_type: agent_plan
executable: false
---

# Implementation Plan: Second Image Upload for Move Clarification

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Implement functionality to allow users to upload a second image from the second player that captures the same chess game. This second image will be used to clarify ambiguous or unclear moves detected in the first image, improving accuracy by cross-referencing moves from both players' perspectives.

## Plan Overview

When a user has already processed a game from one image and encounters unclear or ambiguous moves, they should be able to:
1. Upload a second image from the second player's perspective
2. Process the second image to extract moves
3. Compare and merge moves from both images
4. Use the second image to clarify ambiguous moves from the first image
5. Generate a final, more accurate PGN with resolved ambiguities

## Implementation Plan

### Phase 1: Data Model and Schema Design

**Agent should:**
- Review existing `GameImage` and `ChessGame` models
- Design schema for associating multiple images with a single game
- Add fields to track:
  - Image source (first_player, second_player)
  - Image relationship (primary, secondary)
  - Move confidence scores from each image
  - Ambiguous move indicators
- Design move comparison/merging data structure
- Update database schema or repository layer to support multiple images per game

**Deliverables:**
- Updated data model specification
- Database schema changes (if needed)
- Move comparison data structure design

### Phase 2: Backend Service Layer - Second Image Processing

**Agent should:**
- Create or extend service interface `ISecondImageService` (or extend `GameProcessingService`)
- Implement second image upload endpoint:
  - Accept game ID and second image file
  - Validate that game exists and belongs to user
  - Process second image using existing `ImageExtractionService`
  - Store second image with relationship to primary game
- Implement move comparison logic:
  - Compare moves extracted from both images
  - Identify discrepancies and ambiguities
  - Calculate confidence scores for each move
  - Flag moves that differ between images
- Implement move merging algorithm:
  - Prioritize moves with higher confidence
  - Use second image to clarify ambiguous moves from first
  - Generate merged PGN content
  - Handle cases where moves conflict significantly

**Key Integration Points:**
- Extend `GameProcessingService` or create new service
- Use existing `ImageExtractionService.ProcessImageAsync`
- Integrate with existing validation logic
- Update game record with merged results

**Deliverables:**
- `ISecondImageService` interface (or extended interface)
- Second image processing service implementation
- Move comparison and merging logic
- Updated game processing flow

### Phase 3: Move Comparison and Merging Algorithm

**Agent should implement:**
- Move-by-move comparison:
  - Align moves from both images by move number
  - Compare white and black moves separately
  - Identify exact matches, partial matches, and conflicts
- Ambiguity resolution:
  - Detect ambiguous moves (e.g., "Nbd2" vs "Nfd2")
  - Use second image to resolve ambiguity
  - Prioritize moves that appear in both images
- Confidence scoring:
  - Assign confidence scores based on:
    - OCR quality indicators
    - Move validation status
    - Agreement between images
    - Move legality checks
- Merging strategy:
  - When moves match: use either (prefer higher confidence)
  - When moves differ: use second image to clarify first
  - When moves conflict: flag for manual review or use validation
  - Generate final merged PGN

**Deliverables:**
- Move comparison algorithm
- Ambiguity resolution logic
- Confidence scoring system
- PGN merging implementation

### Phase 4: API Endpoints

**Agent should create:**
- `POST /api/game/{gameId}/second-image` endpoint:
  - Accept multipart form data with image file
  - Accept optional language parameter
  - Process second image
  - Compare and merge with first image
  - Return merged results with comparison data
- `GET /api/game/{gameId}/images` endpoint:
  - Return all images associated with a game
  - Include image metadata and processing results
- `GET /api/game/{gameId}/move-comparison` endpoint:
  - Return detailed move-by-move comparison
  - Show discrepancies and resolutions
- Update existing game endpoints to include second image data

**Deliverables:**
- New API controller endpoints
- Request/response DTOs for second image operations
- Error handling and validation
- API documentation updates

### Phase 5: Backend Testing

**Agent should create:**
- Unit tests for move comparison logic
- Unit tests for move merging algorithm
- Unit tests for ambiguity resolution
- Integration tests for second image upload flow
- Test cases for edge cases:
  - Conflicting moves
  - Different move counts
  - One image has errors
  - Both images have different ambiguities

**Deliverables:**
- Comprehensive test suite
- Test data with sample images
- Integration test coverage

### Phase 6: Frontend Implementation

**Agent should:**

#### 6.1 Update Image Service
- Extend `imageService.ts`:
  - Add `uploadSecondImage(gameId: string, imageFile: File, language: SupportedLanguage): Promise<SecondImageResult>`
  - Add `getGameImages(gameId: string): Promise<GameImageInfo[]>`
  - Add `getMoveComparison(gameId: string): Promise<MoveComparison>`
- Add TypeScript interfaces:
  - `SecondImageResult` (merged PGN, comparison data, confidence scores)
  - `GameImageInfo` (image metadata, source, processing results)
  - `MoveComparison` (move-by-move comparison details)
  - `MoveDiscrepancy` (individual move differences)

#### 6.2 Create Second Image Upload Component
- Create `SecondImageUpload.tsx` component:
  - Display current game information
  - Show upload area for second image
  - Display processing status
  - Show comparison results after processing
  - Allow user to accept/reject merged results
- Integrate with existing `ImageUpload` component patterns
- Use existing UI components (Card, Button, Progress, etc.)

#### 6.3 Create Move Comparison Display Component
- Create `MoveComparisonDisplay.tsx` component:
  - Display side-by-side or tabbed view of moves from both images
  - Highlight discrepancies and conflicts
  - Show confidence scores for each move
  - Display resolved ambiguities
  - Show final merged PGN
- Add visual indicators:
  - Green for matching moves
  - Yellow for ambiguous/resolved moves
  - Red for conflicts
- Make moves interactive (click to see details)

#### 6.4 Update NotationDisplay Component
- Extend `NotationDisplay.tsx`:
  - Add button/link to upload second image
  - Show indicator if second image is available
  - Display merged results when second image is processed
  - Show comparison toggle/view
- Update to handle merged PGN content
- Display confidence indicators

#### 6.5 Update Main Page Flow
- Modify `Index.tsx`:
  - Handle second image upload flow
  - Display comparison view when available
  - Update state management for second image data
- Add navigation between first image, second image, and comparison views

#### 6.6 UI/UX Enhancements
- Add visual feedback:
  - Loading states during second image processing
  - Success/error messages
  - Progress indicators
- Add tooltips explaining:
  - Why second image helps
  - How moves are merged
  - What confidence scores mean
- Responsive design for mobile/tablet/desktop
- Accessibility considerations

**Key Integration Points:**
- Integrate with existing `ImageUpload` and `NotationDisplay` components
- Use existing authentication context
- Follow existing UI patterns and design system
- Maintain consistency with current component structure

**Deliverables:**
- `SecondImageUpload.tsx` component
- `MoveComparisonDisplay.tsx` component
- Updated `imageService.ts` with second image methods
- Updated `NotationDisplay.tsx` with second image support
- Updated `Index.tsx` with second image flow
- TypeScript type definitions
- UI styling and responsive design

## Technical Specifications

### Data Model Extensions
```csharp
// Extend GameImage model or create relationship
public class GameImageRelationship
{
    public Guid PrimaryImageId { get; set; }
    public Guid SecondaryImageId { get; set; }
    public string RelationshipType { get; set; } // "first_player", "second_player"
    public DateTime CreatedAt { get; set; }
}

// Move comparison result
public class MoveComparison
{
    public int MoveNumber { get; set; }
    public string FirstImageMove { get; set; }
    public string SecondImageMove { get; set; }
    public double FirstImageConfidence { get; set; }
    public double SecondImageConfidence { get; set; }
    public string MergedMove { get; set; }
    public string Status { get; set; } // "match", "resolved", "conflict"
    public string ResolutionReason { get; set; }
}
```

### API Endpoints
```typescript
// Backend API endpoints
POST /api/game/{gameId}/second-image
  Body: FormData { image: File, language?: string }
  Response: {
    gameId: string;
    mergedPgn: string;
    comparison: MoveComparison[];
    confidenceScore: number;
    imagesProcessed: number;
  }

GET /api/game/{gameId}/images
  Response: GameImageInfo[]

GET /api/game/{gameId}/move-comparison
  Response: MoveComparison[]
```

### Frontend TypeScript Interfaces
```typescript
// src/services/imageService.ts
export interface SecondImageResult {
  gameId: string;
  mergedPgn: string;
  validation: ValidationResult;
  comparison: MoveComparison[];
  confidenceScore: number;
  imagesProcessed: number;
  processingTime: number;
}

export interface GameImageInfo {
  imageId: string;
  gameId: string;
  source: 'first_player' | 'second_player';
  fileName: string;
  uploadedAt: string;
  pgnContent: string;
  validation: ValidationResult;
}

export interface MoveComparison {
  moveNumber: number;
  firstImageMove: string;
  secondImageMove: string;
  firstImageConfidence: number;
  secondImageConfidence: number;
  mergedMove: string;
  status: 'match' | 'resolved' | 'conflict' | 'ambiguous';
  resolutionReason?: string;
}
```

### Move Merging Algorithm Logic
```
For each move number:
  1. Extract moves from both images
  2. Compare move notation
  3. If exact match:
     - Use move with higher confidence
     - Status: "match"
  4. If partial match (same piece, different square):
     - Use second image to resolve ambiguity
     - Status: "resolved"
  5. If conflict (different moves):
     - Check validation for both
     - Use move that passes validation
     - If both pass, use higher confidence
     - Status: "conflict" or "resolved"
  6. If one image missing move:
     - Use available move
     - Status: "resolved"
  7. Generate merged PGN
```

### Frontend Component Structure
```
src/
  components/
    SecondImageUpload.tsx      // Second image upload component
    MoveComparisonDisplay.tsx   // Comparison view component
  services/
    imageService.ts             // Extended with second image methods
  pages/
    Index.tsx                   // Updated with second image flow
```

## Acceptance Criteria

### Backend
- [ ] Data model supports multiple images per game
- [ ] Second image can be uploaded and associated with existing game
- [ ] Second image is processed using existing image extraction service
- [ ] Move comparison algorithm identifies matches, conflicts, and ambiguities
- [ ] Move merging algorithm generates accurate merged PGN
- [ ] Confidence scoring system works correctly
- [ ] API endpoints for second image upload and comparison
- [ ] Error handling for invalid games, unauthorized access, processing failures
- [ ] Unit tests for move comparison and merging logic
- [ ] Integration tests for second image upload flow

### Frontend
- [ ] `SecondImageUpload.tsx` component created and functional
- [ ] `MoveComparisonDisplay.tsx` component shows move-by-move comparison
- [ ] `imageService.ts` extended with second image methods
- [ ] `NotationDisplay.tsx` updated with second image upload option
- [ ] Second image upload integrated into main flow
- [ ] Comparison view displays discrepancies and resolutions
- [ ] Merged PGN is displayed and can be used
- [ ] Visual indicators for move status (match/resolved/conflict)
- [ ] Loading states and error handling
- [ ] Responsive design for all screen sizes
- [ ] UI follows existing design system

## Dependencies

### Backend
- Existing `GameProcessingService`
- Existing `ImageExtractionService`
- Existing `GameImage` repository
- Existing validation logic
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
This feature significantly improves the accuracy of chess notation extraction by allowing users to cross-reference moves from both players' perspectives. It addresses a common problem where ambiguous moves or unclear handwriting in one image can be clarified using a second image. This will reduce errors, improve user satisfaction, and increase the reliability of the extracted PGN content.

## Effort Estimation

**Effort Level**: 7

**Effort Breakdown**:

### Backend
- Data model and schema design: 2 hours
- Second image processing service: 3 hours
- Move comparison algorithm: 4 hours
- Move merging algorithm: 4 hours
- Confidence scoring system: 2 hours
- API endpoints implementation: 3 hours
- Error handling and edge cases: 2 hours
- Unit tests: 3 hours
- Integration tests: 2 hours
- **Backend Total**: 25 hours

### Frontend
- Second image service and TypeScript interfaces: 2 hours
- `SecondImageUpload.tsx` component: 4 hours
- `MoveComparisonDisplay.tsx` component: 5 hours
- Update `NotationDisplay.tsx`: 2 hours
- Update `Index.tsx` flow: 2 hours
- UI styling and visual indicators: 3 hours
- Error handling and loading states: 2 hours
- Testing and refinement: 3 hours
- **Frontend Total**: 23 hours

**Total Estimated**: 48 hours

## Future Enhancements

This feature establishes the foundation for multi-image processing. Future enhancements may include:
- Support for more than two images
- Automatic image matching (detecting if images are from the same game)
- Machine learning for move confidence prediction
- Batch processing of multiple image pairs
- Export comparison reports
- Manual move editing interface
- Move annotation and notes
