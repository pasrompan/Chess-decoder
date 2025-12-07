---
id: PG-156
status: active
priority_score: .8333
effort: 6
impact: 5
dependencies: []
jira_key: "PG-156"
jira_url: "https://paschalis-rompanos.atlassian.net/browse/PG-156"
created_date: "2025-11-29"
updated_date: 2025-12-07
plan_type: agent_plan
executable: false
---

# Implementation Plan: Project and History File System for Image Uploads

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Design and implement functionality to automatically create a project directory structure and history file for every new image upload. This will enable version tracking and project organization for each processed chess game image, allowing users to maintain a history of changes and iterations for their uploaded images.

## Plan Overview

When a user uploads a new image, the system should:
1. Create a unique project directory (using game ID or timestamp-based identifier)
2. Initialize a history file within the project directory to track all changes, processing steps, and modifications
3. Establish a project structure that supports future versioning and history tracking features

## Implementation Plan

### Phase 1: Project Structure Design

**Agent should:**
- Review existing codebase structure, particularly `GameProcessingService.ProcessGameUploadAsync`
- Design project directory schema (e.g., `projects/{gameId}/history.json`, `projects/{gameId}/images/`)
- Define history file format (recommend JSON for parsing and extensibility)
- Document the project structure specification

**Deliverables:**
- Project structure specification document
- History file schema definition

### Phase 2: Service Layer Implementation

**Agent should:**
- Create a new service interface `IProjectService` (or similar) for project management operations
- Implement project creation logic:
  - Generate unique project identifier (use `ChessGame.Id` as primary identifier)
  - Create project directory structure
  - Initialize history file with initial metadata
- Implement history file update logic for tracking changes
- Add error handling for project/history file creation failures
- Ensure async operations to avoid blocking main upload/processing flow

**Key Integration Points:**
- Integrate with `GameProcessingService.ProcessGameUploadAsync` method
- Hook into the game creation flow after successful image processing
- Consider repository layer updates if needed for project/history file persistence

**Deliverables:**
- `IProjectService` interface
- `ProjectService` implementation
- Project creation and history file management methods

### Phase 3: History File Schema

**Agent should define history file to include:**
- Initial upload metadata:
  - Upload timestamp
  - User ID
  - Original file name and metadata
  - File size, type, storage location
- Processing results:
  - PGN content
  - Validation status
  - Processing time
- Change tracking:
  - Subsequent modification timestamps
  - Change descriptions
  - Version numbers (for future use)

**Deliverables:**
- History file JSON schema
- Example history file structure

### Phase 4: Integration

**Agent should:**
- Modify `GameProcessingService.ProcessGameUploadAsync` to call project creation service
- Ensure project creation happens after successful game/image creation
- Handle rollback scenarios if project creation fails after game creation
- Add logging for project creation operations

**Deliverables:**
- Updated `GameProcessingService` with project creation integration
- Error handling and rollback logic

### Phase 5: Testing

**Agent should create:**
- Unit tests for project creation logic
- Unit tests for history file initialization and updates
- Integration tests for the full upload flow with project creation
- Edge case tests (permissions, disk space, concurrent uploads)

**Deliverables:**
- Test suite for project and history file functionality
- Integration test coverage

### Phase 6: Frontend Implementation

**Agent should:**

#### 6.1 Create Project Service (Frontend)
- Create `projectService.ts` in `src/services/` directory
- Implement API client methods for:
  - `getProjectHistory(projectId: string): Promise<ProjectHistory>`
  - `getProjectInfo(projectId: string): Promise<ProjectInfo>`
  - `getUserProjects(userId: string): Promise<ProjectInfo[]>`
- Add TypeScript interfaces for:
  - `ProjectInfo` (project metadata)
  - `ProjectHistory` (history file structure)
  - `HistoryEntry` (individual version entries)
- Integrate with existing API configuration in `src/config/api.ts`

#### 6.2 Create Project Page Component
- Create new page component `src/pages/Project.tsx`
- Display project information:
  - Project ID and creation date
  - Original upload metadata (file name, size, upload timestamp)
  - Processing results (PGN content, validation status)
  - Processing time and credits used
- Show history timeline/versions list
- Display project status and metadata in a card-based layout
- Use existing UI components from `src/components/ui/` (Card, Badge, Separator, etc.)

#### 6.3 Create Project History Component
- Create `src/components/ProjectHistory.tsx` component
- Display version history as a timeline or list
- Show each version entry with:
  - Version number
  - Timestamp (formatted for readability)
  - Change type badge (initial_upload, modification, etc.)
  - Change description
- Add expandable sections for detailed version information
- Implement filtering/sorting options (by date, version, change type)

#### 6.4 Update Image Upload Flow
- Modify `ImageUpload.tsx` to:
  - Store `projectId` from API response after successful upload
  - Display project creation success message
  - Add optional link/button to view project details
- Update `imageService.ts`:
  - Extend `ProcessedNotation` interface to include `projectId`
  - Update API response handling to capture project information
- Update `Index.tsx`:
  - Add navigation to project page after successful upload
  - Pass project ID to child components when available

#### 6.5 Add Project Navigation
- Add route for project page in routing configuration
- Create navigation link/button in Header or main layout
- Add "View Project" button in `NotationDisplay` component
- Implement project list view (optional, for Phase 1):
  - Create `Projects.tsx` page to list all user projects
  - Add filtering and search capabilities
  - Link to individual project pages

#### 6.6 API Integration
- Create backend API endpoint `GET /api/project/{projectId}/history`
- Create backend API endpoint `GET /api/project/{projectId}`
- Create backend API endpoint `GET /api/project/user/{userId}` (optional)
- Ensure API responses match frontend TypeScript interfaces
- Add error handling for missing projects or unauthorized access

**Key Integration Points:**
- Integrate with existing `ImageUpload` component flow
- Use existing authentication context (`AuthContext`)
- Follow existing UI patterns and component structure
- Maintain consistency with current design system (Tailwind CSS, shadcn/ui components)

**Deliverables:**
- `projectService.ts` with API client methods
- `Project.tsx` page component
- `ProjectHistory.tsx` component
- Updated `ImageUpload.tsx` with project integration
- Updated `imageService.ts` with project data types
- Backend API endpoints for project/history retrieval
- Routing configuration updates
- TypeScript type definitions

## Technical Specifications

### Project Directory Structure
```
projects/
  {gameId}/
    history.json
    images/          (optional, for future use)
      original.jpg
```

### History File Format (JSON)
```json
{
  "projectId": "{gameId}",
  "createdAt": "2025-11-29T10:00:00Z",
  "userId": "{userId}",
  "initialUpload": {
    "fileName": "chess_game.jpg",
    "fileSize": 123456,
    "fileType": "image/jpeg",
    "uploadedAt": "2025-11-29T10:00:00Z",
    "storageLocation": "cloud/local",
    "storageUrl": "..."
  },
  "processing": {
    "processedAt": "2025-11-29T10:00:05Z",
    "pgnContent": "...",
    "validationStatus": "valid/invalid",
    "processingTimeMs": 5000
  },
  "versions": [
    {
      "version": 1,
      "timestamp": "2025-11-29T10:00:00Z",
      "changeType": "initial_upload",
      "description": "Initial image upload and processing"
    }
  ]
}
```

### Service Interface (Proposed)
```csharp
public interface IProjectService
{
    Task<ProjectInfo> CreateProjectAsync(Guid gameId, string userId, GameUploadMetadata metadata);
    Task<HistoryFile> InitializeHistoryFileAsync(Guid projectId, InitialUploadData data);
    Task UpdateHistoryFileAsync(Guid projectId, HistoryEntry entry);
    Task<HistoryFile> GetHistoryFileAsync(Guid projectId);
}
```

### Frontend TypeScript Interfaces
```typescript
// src/services/projectService.ts
export interface ProjectInfo {
  projectId: string;
  gameId: string;
  createdAt: string;
  userId: string;
  initialUpload: {
    fileName: string;
    fileSize: number;
    fileType: string;
    uploadedAt: string;
    storageLocation: string;
    storageUrl?: string;
  };
  processing: {
    processedAt: string;
    pgnContent: string;
    validationStatus: 'valid' | 'invalid';
    processingTimeMs: number;
  };
}

export interface HistoryEntry {
  version: number;
  timestamp: string;
  changeType: 'initial_upload' | 'modification' | 'correction' | 'update';
  description: string;
  changes?: Record<string, any>;
}

export interface ProjectHistory {
  projectId: string;
  createdAt: string;
  userId: string;
  initialUpload: ProjectInfo['initialUpload'];
  processing: ProjectInfo['processing'];
  versions: HistoryEntry[];
}
```

### Frontend API Endpoints
```typescript
// Backend API endpoints to implement:
GET /api/project/{projectId}              // Get project info
GET /api/project/{projectId}/history       // Get project history
GET /api/project/user/{userId}             // Get all user projects (optional)
```

### Frontend Component Structure
```
src/
  pages/
    Project.tsx              // Main project page
    Projects.tsx             // Project list page (optional)
  components/
    ProjectHistory.tsx       // History timeline component
  services/
    projectService.ts        // API client for project operations
```

### Frontend UI Requirements
- Project page should display:
  - Project header with ID and creation date
  - Upload information card (file name, size, timestamp)
  - Processing results card (PGN preview, validation status)
  - History timeline component
  - Navigation back to main upload page
- Use existing design system:
  - shadcn/ui components (Card, Badge, Separator, Button)
  - Tailwind CSS for styling
  - Responsive design for mobile/tablet/desktop
  - Consistent with existing `Index.tsx` and `NotationDisplay.tsx` patterns

## Acceptance Criteria

### Backend
- [ ] Project directory is automatically created for each new image upload
- [ ] Project directory uses unique identifier (game ID)
- [ ] History file is initialized in project directory upon first upload
- [ ] History file tracks initial upload metadata (timestamp, user ID, file name, etc.)
- [ ] History file format is structured JSON and parseable
- [ ] Project directory structure is consistent and follows defined schema
- [ ] History file can be updated when subsequent changes are made
- [ ] Integration with existing `GameProcessingService.ProcessGameUploadAsync` method
- [ ] Error handling for project/history file creation failures
- [ ] Unit tests for project and history file creation logic
- [ ] Integration tests for full upload flow
- [ ] API endpoints for project and history retrieval (`GET /api/project/{projectId}`, `GET /api/project/{projectId}/history`)

### Frontend
- [ ] `projectService.ts` created with API client methods
- [ ] TypeScript interfaces defined for `ProjectInfo`, `ProjectHistory`, and `HistoryEntry`
- [ ] `Project.tsx` page component displays project information
- [ ] `ProjectHistory.tsx` component displays version timeline
- [ ] `ImageUpload.tsx` updated to handle and display project ID
- [ ] `imageService.ts` updated to include project ID in response
- [ ] Navigation to project page after successful upload
- [ ] Project page is accessible via route
- [ ] Error handling for missing projects or API failures
- [ ] Responsive design for mobile, tablet, and desktop
- [ ] UI follows existing design system and component patterns
- [ ] Loading states and error messages implemented

## Dependencies

### Backend
- Existing `GameProcessingService`
- Existing repository layer (may need extensions)
- File system or cloud storage access
- API controller infrastructure

### Frontend
- Existing `ImageUpload` component
- Existing `imageService` API client
- Existing routing configuration
- Authentication context (`AuthContext`)
- UI component library (shadcn/ui components)
- Tailwind CSS for styling

## Impact Assessment

**Impact Level**: Medium

**Impact Description**: 
This feature provides the foundation for version history and project management capabilities. It enables users to track changes over time and organize their chess game uploads into distinct projects. This is a foundational feature that will support future enhancements like version comparison, rollback functionality, and project-based organization of multiple games.

## Effort Estimation

**Effort Level**: 6

**Effort Breakdown**:

### Backend
- Project structure design and specification: 1 hour
- Project directory creation logic: 2 hours
- History file structure and initialization: 2 hours
- Service layer implementation: 2 hours
- Integration with existing upload service: 2 hours
- API endpoints for project/history retrieval: 2 hours
- Error handling and edge cases: 1 hour
- Unit tests: 2 hours
- Integration testing: 1 hour
- **Backend Total**: 15 hours

### Frontend
- Project service and TypeScript interfaces: 1.5 hours
- Project page component (`Project.tsx`): 3 hours
- Project history component (`ProjectHistory.tsx`): 2.5 hours
- Update ImageUpload flow integration: 1.5 hours
- Update imageService with project data: 1 hour
- Routing and navigation setup: 1 hour
- UI styling and responsive design: 2 hours
- Error handling and loading states: 1.5 hours
- Testing and refinement: 2 hours
- **Frontend Total**: 16 hours

**Total Estimated**: 31 hours

## Future Enhancements

This feature establishes infrastructure for version history tracking. Future enhancements may include:
- Version comparison UI
- Rollback to previous versions
- Project-based organization and filtering
- History file visualization
- Export/import of project history
- Project sharing and collaboration features
