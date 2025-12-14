---
id: PG-180
status: completed
priority_score: 1.4000
effort: 5
impact: 7
dependencies: []
created_date: "2025-01-27"
updated_date: 2025-12-07
plan_type: agent_plan
executable: false
---

# Implementation Plan: Auto-detect Language and Remove Language Selector

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Remove the language selector from both frontend and backend, and implement automatic language detection before image processing. This will significantly simplify the user experience by eliminating an unnecessary step while maintaining the same functionality through intelligent auto-detection.

## Plan Overview

The system should:
1. Remove the language selector UI component from the frontend (ImageUpload.tsx)
2. Remove the language parameter from the backend API request model (GameUploadRequest)
3. Implement automatic language detection in the backend before OCR processing
4. Update all service interfaces and implementations to handle auto-detection
5. Update tests to reflect the removal of language parameter

## Implementation Plan

### Phase 1: Backend Language Detection Implementation

**Agent should:**
- Create a new method `DetectLanguageAsync` in `ImageProcessingService` that analyzes the image to determine if it contains Greek or English chess notation
- Implement detection logic that:
  - Performs a quick OCR pass on the image to extract sample text
  - Analyzes the extracted characters to identify language-specific patterns:
    - Greek: Looks for Greek letters (α, β, γ, δ, ε, ζ, η, θ, Π, Α, Β, Ι, Ρ)
    - English: Looks for English piece notation (R, N, B, Q, K) and file letters (a-h)
  - Returns the detected language (defaults to "English" if uncertain)
- Update `ProcessImageAsync` to call `DetectLanguageAsync` before processing
- Update `ExtractMovesFromImageToStringAsync` to use auto-detected language

**Key Integration Points:**
- `ImageProcessingService.cs` - Main service for image processing
- `IImageProcessingService.cs` - Interface definition
- `ImageExtractionService.cs` - Service wrapper
- `IImageExtractionService.cs` - Service interface

**Deliverables:**
- Language detection method implementation
- Updated image processing flow with auto-detection
- Updated service interfaces

### Phase 2: Backend API and Request Model Updates

**Agent should:**
- Remove `Language` property from `GameUploadRequest.cs`
- Update `GameController.cs` to remove language parameter handling
- Update `GameProcessingService.cs` to remove language from request processing
- Update all method signatures that accept language parameter to use auto-detection instead
- Ensure backward compatibility is not required (breaking change is acceptable)

**Key Integration Points:**
- `DTOs/Requests/GameUploadRequest.cs` - Request model
- `Controllers/GameController.cs` - API endpoint
- `Services/GameProcessing/GameProcessingService.cs` - Processing service
- `Services/ImageProcessing/ImageExtractionService.cs` - Image extraction service

**Deliverables:**
- Updated request model without language field
- Updated controller and service methods
- Removed language parameter from all relevant method signatures

### Phase 3: Frontend Language Selector Removal

**Agent should:**
- Remove the language state variable and Select component from `ImageUpload.tsx`
- Remove the language import if it's only used for the selector
- Update `imageService.ts` to remove language parameter from `processImage` function
- Remove language from FormData being sent to the API
- Update TypeScript interfaces/types if language is part of any type definitions

**Key Integration Points:**
- `src/components/ImageUpload.tsx` - Main upload component
- `src/services/imageService.ts` - API service
- Any type definitions that include language

**Deliverables:**
- Updated ImageUpload component without language selector
- Updated imageService without language parameter
- Cleaned up unused imports and types

### Phase 4: Testing and Validation

**Agent should:**

#### 4.1 Backend Unit Tests
- Update existing unit tests to remove language parameter
- Add unit tests for language detection functionality
- Test detection accuracy with both Greek and English notation samples
- Test fallback to English when detection is uncertain
- Update `GameControllerTests.cs` to remove language from test requests
- Update `GameProcessingMetadataTests.cs` to remove language assertions

#### 4.2 Integration Tests
- Test end-to-end image processing with auto-detection
- Verify Greek notation is correctly detected and processed
- Verify English notation is correctly detected and processed
- Test edge cases (mixed notation, unclear notation)

#### 4.3 Frontend Tests
- Verify UI no longer shows language selector
- Verify image upload still works correctly
- Test that processing completes successfully without language selection

**Key Integration Points:**
- `Tests/Controllers/GameControllerTests.cs`
- `Tests/Services/GameProcessingMetadataTests.cs`
- `EvaluationRunner/` - Update evaluation scripts if they use language parameter

**Deliverables:**
- Updated unit tests
- Integration test coverage for auto-detection
- Verified frontend functionality

## Technical Specifications

### Backend Language Detection Method
```csharp
// ImageProcessingService.cs
/// <summary>
/// Automatically detects the language of chess notation in an image
/// </summary>
/// <param name="imagePath">Path to the image file</param>
/// <returns>Detected language ("Greek" or "English", defaults to "English")</returns>
private async Task<string> DetectLanguageAsync(string imagePath)
{
    // Implementation:
    // 1. Read image bytes
    // 2. Perform quick OCR pass to extract sample text
    // 3. Analyze for Greek characters (α-θ, Π, Α, Β, Ι, Ρ)
    // 4. Analyze for English notation (R, N, B, Q, K, a-h)
    // 5. Return detected language or default to "English"
}
```

### Updated Request Model
```csharp
// DTOs/Requests/GameUploadRequest.cs
public class GameUploadRequest
{
    [Required]
    public IFormFile Image { get; set; } = null!;

    [Required]
    public string UserId { get; set; } = string.Empty;

    // Language property removed - now auto-detected

    public bool AutoCrop { get; set; } = false;

    // Optional player metadata for PGN format
    public string? WhitePlayer { get; set; }
    public string? BlackPlayer { get; set; }
    public DateTime? GameDate { get; set; }
    public string? Round { get; set; }
}
```

### Updated Service Interface
```csharp
// IImageProcessingService.cs
public interface IImageProcessingService
{
    Task<ChessGameResponse> ProcessImageAsync(string imagePath, PgnMetadata? metadata = null);
    // Language parameter removed - auto-detected internally
}
```

### Frontend Service Update
```typescript
// src/services/imageService.ts
export const processImage = async (
  imageFile: File, 
  userId: string,
  autoCrop: boolean = false,
  metadata?: PgnMetadata
): Promise<ProcessedNotation> => {
  // Language parameter removed from function signature and FormData
}
```

### Frontend Component Update
```typescript
// src/components/ImageUpload.tsx
// Remove:
// - const [language, setLanguage] = useState<SupportedLanguage>("English");
// - Language Select component (lines ~519-533)
// - language parameter from processImage call
```

## Acceptance Criteria

### Backend
- [ ] Language detection method correctly identifies Greek notation (≥95% accuracy)
- [ ] Language detection method correctly identifies English notation (≥95% accuracy)
- [ ] Language detection defaults to "English" when uncertain
- [ ] `GameUploadRequest` no longer contains `Language` property
- [ ] All service methods updated to use auto-detection
- [ ] All unit tests updated and passing
- [ ] Integration tests verify auto-detection works end-to-end

### Frontend
- [ ] Language selector UI component removed from ImageUpload
- [ ] No language state or related imports remain
- [ ] `imageService.processImage` no longer accepts language parameter
- [ ] Image upload and processing works without language selection
- [ ] UI is cleaner and more streamlined
- [ ] No console errors or warnings related to language

## Dependencies

### Backend
- `ImageProcessingService` - Core image processing service
- `IImageProcessingService` - Service interface
- `ImageExtractionService` - Service wrapper
- OCR providers (Gemini/OpenAI) - For initial text extraction for detection
- Existing chess notation parsing logic

### Frontend
- `ImageUpload.tsx` - Main upload component
- `imageService.ts` - API service layer
- shadcn/ui Select component (to be removed)

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
This change significantly improves user experience by removing an unnecessary manual step. Users no longer need to know or select the language of their chess notation - the system will automatically detect it. This reduces cognitive load and potential errors from incorrect language selection. The change affects both frontend UI and backend processing, but maintains all existing functionality through intelligent auto-detection.

**GTM Alignment**:
- **Activation Support**: Removing friction in the upload flow improves first-upload success rate
- **User Experience**: Simpler onboarding reduces abandonment during first upload
- **Error Prevention**: Auto-detection prevents user errors from incorrect language selection

## Effort Estimation

**Effort Level**: 5 (Moderate complexity)

**Effort Breakdown**:

### Backend
- Language detection implementation: 4 hours
- Update request models and services: 2 hours
- Update all method signatures: 1 hour
- Unit test updates and new detection tests: 3 hours
- Integration testing: 2 hours
- **Backend Total**: 12 hours

### Frontend
- Remove language selector UI: 1 hour
- Update service layer: 1 hour
- Clean up types and imports: 0.5 hours
- Testing and verification: 1 hour
- **Frontend Total**: 3.5 hours

**Total Estimated**: 15.5 hours

## Future Enhancements

This feature establishes automatic language detection as the default approach. Future enhancements may include:
- Support for additional languages (Spanish, French, German, etc.)
- Confidence scoring for language detection
- Ability to override auto-detection if needed (advanced user setting)
- Learning from user corrections to improve detection accuracy
- Multi-language support in a single notation (though this is rare)

