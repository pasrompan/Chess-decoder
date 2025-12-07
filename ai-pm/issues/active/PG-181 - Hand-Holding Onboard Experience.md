---
id: PG-181
status: active
priority_score: 1.5000
effort: 6
impact: 9
dependencies: []
created_date: "2025-01-27"
updated_date: 2025-12-07
plan_type: agent_plan
executable: false
---

# Implementation Plan: Hand-Holding Onboard Experience

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Implement a comprehensive onboarding experience that educates users on how to take high-quality photos of chess scoresheets, provides real-time feedback on image quality, and offers a sample image option to demonstrate the application's 90% accuracy potential. This addresses the critical gap where users experience poor results (~10% accuracy) due to suboptimal image quality, leading to churn despite the application's capability to achieve 50-90% accuracy with proper inputs.

## Plan Overview

The system should:
1. Display an educational modal/carousel before the first upload showing "How to take a perfect photo" with Good vs. Bad examples (glare, shadows, angle issues)
2. Implement a feedback loop that detects low-confidence images and prompts users with specific reasons to retry
3. Provide a "Try with a Sample Image" option that demonstrates the 90% accuracy potential immediately
4. Track first-time user status to show onboarding only once
5. Integrate seamlessly with existing ImageUpload component without disrupting current workflow

## Implementation Plan

### Phase 1: Education Modal/Carousel Component

**Agent should:**
- Create a new `PhotoQualityGuideDialog` component that displays before the first image upload
- Design a carousel/slideshow showing:
  - **Slide 1**: Introduction - "Get the best results by following these tips"
  - **Slide 2**: Good Photo Example - Clear, well-lit, flat scoresheet
  - **Slide 3**: Bad Photo Examples - Side-by-side comparison:
    - Glare/reflection issues
    - Poor lighting/shadows
    - Angled perspective
    - Blurry image
  - **Slide 4**: Best Practices Checklist:
    - Use natural lighting or avoid harsh shadows
    - Keep the scoresheet flat
    - Ensure the entire scoresheet is visible
    - Avoid glare from overhead lights
    - Take photo from directly above
- Add "Don't show again" checkbox with localStorage persistence
- Add navigation (Previous/Next buttons, dots indicator)
- Style using existing shadcn/ui Dialog component and Tailwind CSS
- Make it responsive for mobile devices

**Key Integration Points:**
- `src/components/ImageUpload.tsx` - Main upload component
- `src/components/ui/dialog.tsx` - Dialog component from shadcn/ui
- localStorage API - For "don't show again" preference

**Deliverables:**
- `PhotoQualityGuideDialog.tsx` component
- Example images (good/bad) stored in `public/` directory
- Integration with ImageUpload component

### Phase 2: Sample Image Feature

**Agent should:**
- Create a high-quality sample chess scoresheet image demonstrating 90% accuracy
- Add a "Try with Sample Image" button/option in the ImageUpload component
- When clicked, automatically load and process the sample image
- Display a brief message: "This is a sample image showing our best accuracy. Your results may vary based on image quality."
- Ensure the sample image processing follows the same flow as user uploads
- Store sample image in `public/sample-images/` directory

**Key Integration Points:**
- `src/components/ImageUpload.tsx` - Add sample image button
- `src/services/imageService.ts` - Process sample image through same service
- `public/sample-images/` - Sample image storage

**Deliverables:**
- Sample chess scoresheet image (high quality, clear notation)
- "Try with Sample Image" UI element
- Sample image processing integration

### Phase 3: Image Quality Feedback Loop

**Agent should:**
- Research and implement client-side image quality detection:
  - **Blur Detection**: Use Laplacian variance or similar algorithm
  - **Contrast Detection**: Analyze image histogram for contrast levels
  - **Brightness Detection**: Check for over/under-exposed images
  - **Aspect Ratio Check**: Verify image isn't too distorted
- Create `ImageQualityAnalyzer` utility/service:
  - Accepts image file or data URL
  - Returns quality metrics: `{ blurScore, contrastScore, brightnessScore, overallQuality: 'good' | 'fair' | 'poor', issues: string[] }`
- Integrate quality check before image upload:
  - If quality is 'poor', show warning dialog with specific issues
  - Provide actionable feedback: "This image might yield poor results due to [specific reason]. Try again?"
  - Allow user to proceed anyway or retry
- Optionally integrate with backend confidence scores if available:
  - If backend returns low confidence, show follow-up feedback after processing

**Key Integration Points:**
- `src/components/ImageUpload.tsx` - Quality check before upload
- `src/utils/imageQualityAnalyzer.ts` - New utility for quality analysis
- Backend API response (if confidence scores are available)

**Deliverables:**
- `ImageQualityAnalyzer` utility with blur/contrast/brightness detection
- Quality feedback dialog component
- Integration with upload flow

### Phase 4: First-Time User Detection and Onboarding Flow

**Agent should:**
- Track first-time user status using localStorage:
  - Key: `chessDecoder_firstTimeUser` (boolean)
  - Set to `false` after first upload or after dismissing guide
- Modify ImageUpload component to:
  - Check first-time user status on mount
  - Show PhotoQualityGuideDialog if first-time user
  - Show sample image option prominently for first-time users
- Add onboarding state management:
  - Track if user has seen the guide
  - Track if user has tried sample image
  - Allow users to access guide again via help/info button
- **GTM Analytics Integration** (for activation rate tracking):
  - Track event when quality guide is shown
  - Track event when quality guide is dismissed (with "don't show again" status)
  - Track event when sample image is used
  - Track event when first upload is attempted (after viewing guide)
  - Send analytics events to backend for KPI tracking (optional, can use localStorage initially)

**Key Integration Points:**
- `src/components/ImageUpload.tsx` - First-time user detection
- localStorage API - User preference storage
- `PhotoQualityGuideDialog.tsx` - Onboarding modal

**Deliverables:**
- First-time user detection logic
- Onboarding flow integration
- Help/info button to re-access guide

### Phase 5: Backend Confidence Score Integration (Optional Enhancement)

**Agent should:**
- Investigate if backend can provide confidence scores for OCR results
- If available, add confidence score to API response:
  - Update `ProcessedNotation` interface to include optional `confidenceScore?: number`
  - Update backend response DTO if needed
- Display confidence feedback in UI:
  - If confidence < 0.5, show warning: "Low confidence detected. Results may need more editing."
  - Suggest retrying with better image quality
  - Highlight moves that need review

**Key Integration Points:**
- `src/services/imageService.ts` - API response interface
- Backend `ChessGameResponse` DTO - Add confidence field
- `src/components/NotationDisplay.tsx` - Display confidence indicators

**Deliverables:**
- Confidence score in API response (if backend supports)
- UI indicators for low-confidence results
- User guidance based on confidence levels

## Technical Specifications

### PhotoQualityGuideDialog Component
```typescript
// src/components/PhotoQualityGuideDialog.tsx
interface PhotoQualityGuideDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDontShowAgain?: (value: boolean) => void;
}

export const PhotoQualityGuideDialog: React.FC<PhotoQualityGuideDialogProps> = ({
  open,
  onOpenChange,
  onDontShowAgain
}) => {
  // Carousel implementation with:
  // - Multiple slides showing good/bad examples
  // - Navigation controls
  // - "Don't show again" checkbox
  // - Responsive design
}
```

### ImageQualityAnalyzer Utility
```typescript
// src/utils/imageQualityAnalyzer.ts
export interface ImageQualityMetrics {
  blurScore: number; // 0-1, higher is better
  contrastScore: number; // 0-1, higher is better
  brightnessScore: number; // 0-1, 0.5 is optimal
  overallQuality: 'good' | 'fair' | 'poor';
  issues: string[]; // e.g., ["Low contrast", "Image is blurry"]
}

export async function analyzeImageQuality(
  imageFile: File | string
): Promise<ImageQualityMetrics> {
  // Implementation:
  // 1. Load image to canvas
  // 2. Calculate Laplacian variance for blur detection
  // 3. Analyze histogram for contrast
  // 4. Calculate average brightness
  // 5. Return quality metrics
}
```

### Sample Image Integration
```typescript
// src/components/ImageUpload.tsx
const handleSampleImage = async () => {
  // Load sample image from public/sample-images/
  const sampleImageUrl = '/sample-images/chess-scoresheet-sample.jpg';
  // Process through same flow as user upload
  // Show message about sample accuracy
};
```

### First-Time User Detection
```typescript
// src/components/ImageUpload.tsx
useEffect(() => {
  const isFirstTime = localStorage.getItem('chessDecoder_firstTimeUser') !== 'false';
  if (isFirstTime) {
    setShowQualityGuide(true);
  }
}, []);

const handleFirstUpload = () => {
  localStorage.setItem('chessDecoder_firstTimeUser', 'false');
};
```

### Updated ProcessedNotation Interface (if confidence available)
```typescript
// src/services/imageService.ts
export interface ProcessedNotation {
  // ... existing fields
  confidenceScore?: number; // 0-1, optional if backend provides
  qualityWarnings?: string[]; // Optional quality warnings
}
```

## Acceptance Criteria

### Frontend - Education Modal
- [ ] PhotoQualityGuideDialog component displays before first upload for new users
- [ ] Carousel shows at least 4 slides: Introduction, Good Example, Bad Examples, Best Practices
- [ ] Good vs. Bad examples are clearly visible and labeled
- [ ] "Don't show again" checkbox works and persists to localStorage
- [ ] Modal is responsive and works on mobile devices
- [ ] Navigation (Previous/Next/Dots) works correctly
- [ ] Modal can be dismissed and re-opened via help button

### Frontend - Sample Image
- [ ] "Try with Sample Image" button is visible and accessible
- [ ] Sample image is high quality and demonstrates 90% accuracy
- [ ] Sample image processes through same flow as user uploads
- [ ] Message explains this is a sample showing best-case accuracy
- [ ] Sample image option is prominently displayed for first-time users

### Frontend - Quality Feedback
- [ ] ImageQualityAnalyzer detects blur, contrast, and brightness issues
- [ ] Quality check runs before image upload
- [ ] Warning dialog appears for poor quality images with specific reasons
- [ ] User can proceed anyway or retry with better image
- [ ] Feedback messages are actionable and clear
- [ ] Quality analysis completes quickly (< 1 second)

### Frontend - First-Time User Flow
- [ ] First-time users see quality guide on first visit
- [ ] Guide doesn't show again after being dismissed with "don't show again"
- [ ] First-time user status persists across sessions
- [ ] Help/info button allows re-accessing the guide
- [ ] Sample image is prominently suggested for first-time users

### Integration
- [ ] All features integrate seamlessly with existing ImageUpload component
- [ ] No breaking changes to existing functionality
- [ ] Upload flow remains smooth and intuitive
- [ ] Mobile experience is optimized

### Optional - Backend Confidence
- [ ] Backend returns confidence scores if available
- [ ] Low confidence results show appropriate warnings
- [ ] Confidence indicators are displayed in NotationDisplay component

## Dependencies

### Frontend
- `ImageUpload.tsx` - Main upload component to integrate with
- `Dialog` component from shadcn/ui - For modals
- `Button`, `Card` components from shadcn/ui - For UI elements
- Canvas API - For image quality analysis
- localStorage API - For user preferences
- Sample chess scoresheet image - To be created/obtained

### Backend (Optional)
- Confidence score in API response - If backend can provide OCR confidence
- `ChessGameResponse` DTO - May need update for confidence field

### Assets
- Good example chess scoresheet photo
- Bad example photos (glare, shadows, angle, blurry)
- High-quality sample scoresheet for demo

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
This feature directly addresses the primary user churn issue: poor results due to suboptimal image quality. By educating users upfront and providing real-time feedback, we bridge the gap between the "wow" demo experience and the self-serve user experience. The sample image feature immediately demonstrates the application's true potential (90% accuracy), building trust and setting proper expectations. This is critical for Phase 1 of the GTM strategy: Activation & Value Proof. Success here directly translates to higher activation rates, better retention, and user satisfaction.

**GTM Alignment**:
- **Primary GTM Feature**: This is the #1 priority feature for Phase 1 (Activation & Value Proof)
- **Activation Rate Impact**: Expected to increase activation rate from ~10% to ≥60% by ensuring users get good results on first upload
- **Outreach Readiness**: Must be completed before launching to local chess club
- **Key Metrics to Track**:
  - Quality guide view rate (target: ≥80% of first-time users)
  - Sample image usage rate (target: ≥40% of first-time users)
  - First upload success rate after viewing guide (target: ≥80%)
  - First upload accuracy improvement (target: ≥50% accuracy)

## Effort Estimation

**Effort Level**: 6 (Moderate to High complexity)

**Effort Breakdown**:

### Frontend
- PhotoQualityGuideDialog component development: 6 hours
  - Carousel implementation: 2 hours
  - Good/bad example images (creation/curation): 2 hours
  - Styling and responsive design: 2 hours
- Sample image feature: 3 hours
  - Sample image creation/selection: 1 hour
  - UI integration: 1 hour
  - Processing integration: 1 hour
- ImageQualityAnalyzer utility: 8 hours
  - Blur detection algorithm: 3 hours
  - Contrast/brightness analysis: 2 hours
  - Integration and testing: 3 hours
- Quality feedback dialog: 3 hours
  - Dialog component: 1 hour
  - Actionable messaging: 1 hour
  - Integration with upload flow: 1 hour
- First-time user detection and flow: 2 hours
  - localStorage logic: 0.5 hours
  - Onboarding state management: 1 hour
  - Help button integration: 0.5 hours
- Integration and testing: 4 hours
  - End-to-end testing: 2 hours
  - Mobile testing: 1 hour
  - Bug fixes and refinements: 1 hour
- **Frontend Total**: 26 hours

### Backend (Optional)
- Confidence score implementation: 4 hours
  - Add confidence to OCR processing: 2 hours
  - Update response DTO: 1 hour
  - Testing: 1 hour
- **Backend Total**: 4 hours (optional)

**Total Estimated**: 26-30 hours (depending on backend confidence feature)

## Future Enhancements

This feature establishes the foundation for user education and quality feedback. Future enhancements may include:
- **Advanced Quality Detection**: Use ML models for more sophisticated image quality assessment
- **Real-time Camera Preview Feedback**: Show quality indicators while user is taking photo
- **Interactive Tutorial**: Step-by-step walkthrough with user's own image
- **Community Examples**: Gallery of user-submitted good/bad examples
- **Personalized Tips**: ML-based suggestions based on user's common mistakes
- **A/B Testing Framework**: Test different onboarding flows to optimize activation
- **Video Tutorial**: Short video showing best practices
- **Multi-language Support**: Translate quality guide for international users
- **Quality Score History**: Track and display user's image quality improvements over time

