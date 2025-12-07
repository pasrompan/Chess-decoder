---
id: PG-177
status: active
priority_score: 1.2500
effort: 4
impact: 5
dependencies: []
jira_key: "PG-177"
jira_url: "https://paschalis-rompanos.atlassian.net/browse/PG-177"
created_date: "2025-11-29"
updated_date: 2025-12-07
plan_type: agent_plan
executable: false
---

# Implementation Plan: Player Details in PGN Format

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Implement functionality to allow users to input and store player information (White player name, Black player name, Date, and Round) that will be included in the generated PGN format. Currently, these fields are hardcoded with placeholder values ("??" or "?"). This feature will enable users to personalize their chess game records with actual player names and game metadata.

## Plan Overview

The system should allow users to:
1. Input player names (White and Black) during or after image upload
2. Input game date (with default to current date)
3. Input round number (optional)
4. Store this metadata with the game
5. Include this information in the generated PGN format
6. Edit player details after initial game creation

## Implementation Plan

### Phase 1: Data Model and Schema Design

**Agent should:**
- Review existing `ChessGame` model structure
- Add new fields to store player metadata:
  - `WhitePlayer` (string, nullable)
  - `BlackPlayer` (string, nullable)
  - `GameDate` (DateTime, nullable)
  - `Round` (string, nullable)
- Update database schema/migrations if needed
- Ensure backward compatibility (existing games should have null values)
- Update Firestore data model annotations if using Firestore

**Deliverables:**
- Updated `ChessGame` model with new fields
- Database migration scripts (if using SQL)
- Data model documentation

### Phase 2: Backend Service Layer - PGN Generation Updates

**Agent should:**
- Update `GeneratePGNContentAsync` method in `ImageProcessingService`:
  - Accept optional `PgnMetadata` parameter with player details
  - Use provided metadata instead of hardcoded placeholders
  - Format Date as "yyyy.MM.dd" or "????.??.??" if not provided
  - Include Round field if provided
  - Use actual player names or "?" if not provided
- Create `PgnMetadata` class/DTO:
  ```csharp
  public class PgnMetadata
  {
      public string? WhitePlayer { get; set; }
      public string? BlackPlayer { get; set; }
      public DateTime? GameDate { get; set; }
      public string? Round { get; set; }
  }
  ```
- Update `GameProcessingService.ProcessGameUploadAsync`:
  - Accept optional player metadata in request
  - Store metadata in `ChessGame` entity
  - Pass metadata to PGN generation
- Update game update/edit endpoints to allow modifying player details

**Key Integration Points:**
- Modify `IImageProcessingService.GeneratePGNContentAsync` signature
- Update `GameProcessingService` to handle metadata
- Ensure existing code continues to work with default values

**Deliverables:**
- Updated `GeneratePGNContentAsync` method
- `PgnMetadata` DTO class
- Updated game processing service
- Backward compatibility maintained

### Phase 3: API Endpoints

**Agent should create/update:**
- Update `POST /api/game/upload` endpoint:
  - Accept optional fields in request body:
    - `whitePlayer` (string, optional)
    - `blackPlayer` (string, optional)
    - `gameDate` (string/DateTime, optional)
    - `round` (string, optional)
  - Validate date format if provided
  - Store metadata with game
- Create/update `PUT /api/game/{gameId}/metadata` endpoint:
  - Allow updating player details after game creation
  - Validate input
  - Regenerate PGN with new metadata
- Update `GET /api/game/{gameId}` endpoint:
  - Return player metadata in response
- Create request DTOs:
  - `GameUploadRequest` extended with metadata fields
  - `UpdateGameMetadataRequest` DTO

**Deliverables:**
- Updated API endpoints
- Request/response DTOs
- API validation logic
- Error handling

### Phase 4: Backend Testing

**Agent should create:**
- Unit tests for PGN generation with metadata:
  - Test with all fields provided
  - Test with partial fields
  - Test with no fields (backward compatibility)
  - Test date formatting
- Unit tests for metadata storage and retrieval
- Integration tests for game upload with metadata
- Integration tests for metadata update endpoint
- Edge case tests:
  - Invalid date formats
  - Very long player names
  - Special characters in names

**Deliverables:**
- Comprehensive test suite
- Test coverage for all scenarios
- Edge case validation

### Phase 5: Frontend Implementation

**Agent should:**

#### 5.1 Update Image Service
- Extend `imageService.ts`:
  - Update `processImage` function to accept optional metadata:
    ```typescript
    export const processImage = async (
      imageFile: File,
      language: SupportedLanguage,
      userId: string,
      autoCrop: boolean,
      metadata?: PgnMetadata
    ): Promise<ProcessedNotation>
    ```
  - Add `updateGameMetadata(gameId: string, metadata: PgnMetadata): Promise<void>`
- Add TypeScript interfaces:
  ```typescript
  export interface PgnMetadata {
    whitePlayer?: string;
    blackPlayer?: string;
    gameDate?: string; // ISO date string
    round?: string;
  }
  ```

#### 5.2 Create Player Details Input Component
- Create `PlayerDetailsForm.tsx` component:
  - Form fields for:
    - White player name (text input)
    - Black player name (text input)
    - Game date (date picker, default to today)
    - Round (text input, optional)
  - Validation:
    - Player names: max length, trim whitespace
    - Date: valid date format
    - Round: optional, alphanumeric
  - Use existing UI components (Input, Label, Button from shadcn/ui)
  - Responsive design for mobile/tablet/desktop

#### 5.3 Update Image Upload Flow
- Modify `ImageUpload.tsx`:
  - Add optional "Add Player Details" section/accordion
  - Integrate `PlayerDetailsForm` component
  - Pass metadata to `processImage` function
  - Show metadata in upload summary
  - Allow editing metadata before submission
- Add toggle/checkbox to show/hide player details form
- Store metadata in component state

#### 5.4 Update Notation Display
- Modify `NotationDisplay.tsx`:
  - Display player names and metadata if available
  - Show "Edit Player Details" button/link
  - Allow editing metadata after game creation
  - Update PGN display when metadata changes
- Add metadata display section:
  - Show White/Black player names
  - Show game date
  - Show round (if provided)

#### 5.5 Create Metadata Edit Dialog/Modal
- Create `EditPlayerDetailsDialog.tsx` component:
  - Reusable form for editing player details
  - Pre-populate with existing values
  - Save changes via API
  - Update displayed PGN after save
  - Show loading states and error handling
- Use existing Dialog component from shadcn/ui

#### 5.6 Update Main Page Flow
- Modify `Index.tsx`:
  - Handle metadata state
  - Pass metadata through component hierarchy
  - Update PGN display when metadata changes

**Key Integration Points:**
- Integrate with existing `ImageUpload` component
- Use existing form components and patterns
- Follow existing UI/UX patterns
- Maintain consistency with design system

**Deliverables:**
- `PlayerDetailsForm.tsx` component
- `EditPlayerDetailsDialog.tsx` component
- Updated `imageService.ts` with metadata support
- Updated `ImageUpload.tsx` with metadata input
- Updated `NotationDisplay.tsx` with metadata display
- Updated `Index.tsx` with metadata flow
- TypeScript type definitions

## Technical Specifications

### PGN Format Output
```
[Date "2025.11.29"]        // or "????.??.??" if not provided
[Round "1"]                // or omitted if not provided
[White "John Doe"]         // or "?" if not provided
[Black "Jane Smith"]       // or "?" if not provided
[Result "*"]
```

### Data Model Extensions
```csharp
// Extend ChessGame model
public class ChessGame
{
    // ... existing fields ...
    
    [FirestoreProperty]
    public string? WhitePlayer { get; set; }
    
    [FirestoreProperty]
    public string? BlackPlayer { get; set; }
    
    [FirestoreProperty]
    public DateTime? GameDate { get; set; }
    
    [FirestoreProperty]
    public string? Round { get; set; }
}
```

### API Request/Response
```typescript
// POST /api/game/upload
Request: FormData {
  image: File
  language: string
  userId: string
  autoCrop: boolean
  whitePlayer?: string      // NEW
  blackPlayer?: string      // NEW
  gameDate?: string         // NEW (ISO format)
  round?: string            // NEW
}

// PUT /api/game/{gameId}/metadata
Request: {
  whitePlayer?: string
  blackPlayer?: string
  gameDate?: string
  round?: string
}
```

### Frontend TypeScript Interfaces
```typescript
// src/services/imageService.ts
export interface PgnMetadata {
  whitePlayer?: string;
  blackPlayer?: string;
  gameDate?: string;  // ISO date string (YYYY-MM-DD)
  round?: string;
}

export interface ProcessedNotation {
  // ... existing fields ...
  metadata?: PgnMetadata;  // NEW
}
```

### PGN Generation Logic
```csharp
public string GeneratePGNContentAsync(
    IEnumerable<string> whiteMoves, 
    IEnumerable<string> blackMoves,
    PgnMetadata? metadata = null)
{
    var sb = new StringBuilder();
    
    // Date
    if (metadata?.GameDate.HasValue == true)
    {
        sb.AppendLine($"[Date \"{metadata.GameDate.Value:yyyy.MM.dd}\"]");
    }
    else
    {
        sb.AppendLine("[Date \"????.??.??\"]");
    }
    
    // Round (optional)
    if (!string.IsNullOrWhiteSpace(metadata?.Round))
    {
        sb.AppendLine($"[Round \"{metadata.Round}\"]");
    }
    
    // White player
    sb.AppendLine($"[White \"{metadata?.WhitePlayer ?? "?"}\"]");
    
    // Black player
    sb.AppendLine($"[Black \"{metadata?.BlackPlayer ?? "?"}\"]");
    
    sb.AppendLine("[Result \"*\"]");
    sb.AppendLine();
    
    // ... existing move generation logic ...
}
```

### Frontend Component Structure
```
src/
  components/
    PlayerDetailsForm.tsx        // Form for inputting player details
    EditPlayerDetailsDialog.tsx  // Dialog for editing metadata
  services/
    imageService.ts              // Extended with metadata support
  pages/
    Index.tsx                    // Updated with metadata flow
```

## Acceptance Criteria

### Backend
- [ ] `ChessGame` model extended with player metadata fields
- [ ] Database schema updated (migration if needed)
- [ ] `GeneratePGNContentAsync` accepts and uses metadata
- [ ] PGN format includes Date, Round (if provided), White, Black
- [ ] Default values used when metadata not provided ("?" or "????.??.??")
- [ ] `POST /api/game/upload` accepts optional metadata fields
- [ ] `PUT /api/game/{gameId}/metadata` allows updating metadata
- [ ] Metadata is stored and retrieved correctly
- [ ] Backward compatibility maintained (existing games work)
- [ ] Date validation and formatting
- [ ] Unit tests for PGN generation with metadata
- [ ] Integration tests for metadata endpoints

### Frontend
- [ ] `PlayerDetailsForm.tsx` component created
- [ ] `EditPlayerDetailsDialog.tsx` component created
- [ ] `imageService.ts` extended with metadata support
- [ ] `ImageUpload.tsx` includes player details input
- [ ] `NotationDisplay.tsx` shows player metadata
- [ ] Metadata can be edited after game creation
- [ ] PGN updates when metadata changes
- [ ] Form validation for all fields
- [ ] Date picker works correctly
- [ ] Default date is current date
- [ ] Responsive design for all screen sizes
- [ ] Error handling and loading states
- [ ] UI follows existing design system

## Dependencies

### Backend
- Existing `ChessGame` model
- Existing `ImageProcessingService`
- Existing `GameProcessingService`
- API controller infrastructure

### Frontend
- Existing `ImageUpload` component
- Existing `NotationDisplay` component
- Existing `imageService` API client
- UI component library (shadcn/ui)
- Date picker component (may need to add)
- Tailwind CSS for styling

## Impact Assessment

**Impact Level**: Medium

**Impact Description**: 
This feature enhances the value of generated PGN files by allowing users to include meaningful metadata about their games. It improves the professional appearance of exported games and makes them more useful for record-keeping, sharing, and analysis. The feature is user-friendly and optional, so it doesn't disrupt existing workflows while providing additional functionality for users who want to personalize their game records.

## Effort Estimation

**Effort Level**: 4

**Effort Breakdown**:

### Backend
- Data model and schema updates: 1.5 hours
- PGN generation method updates: 2 hours
- Service layer updates: 1.5 hours
- API endpoints implementation: 2 hours
- Validation and error handling: 1 hour
- Unit tests: 2 hours
- Integration tests: 1.5 hours
- **Backend Total**: 11.5 hours

### Frontend
- TypeScript interfaces and service updates: 1 hour
- `PlayerDetailsForm.tsx` component: 3 hours
- `EditPlayerDetailsDialog.tsx` component: 2 hours
- Update `ImageUpload.tsx`: 2 hours
- Update `NotationDisplay.tsx`: 2 hours
- Update `Index.tsx` flow: 1 hour
- Form validation and date picker: 2 hours
- UI styling and responsive design: 2 hours
- Testing and refinement: 2 hours
- **Frontend Total**: 17 hours

**Total Estimated**: 28.5 hours

## Future Enhancements

This feature establishes the foundation for game metadata. Future enhancements may include:
- Player database/autocomplete (suggest players from previous games)
- Tournament information fields
- Event and Site fields in PGN
- Result field (instead of always "*")
- Bulk metadata editing for multiple games
- Metadata templates/presets
- Export metadata separately
- Import metadata from existing PGN files
- Player statistics and history

