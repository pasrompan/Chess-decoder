---
id: PG-179
status: completed
priority_score: 1.3333
effort: 6
impact: 8
dependencies: []
created_date: "2025-12-07"
updated_date: "2025-12-07"
plan_type: agent_plan
executable: false
---

# Implementation Plan: E2E Test Suite with Playwright

> **NOTE**: This is a planning document for an agent. This plan is NOT to be executed automatically. It serves as a reference and specification for future implementation.

## Objective

Create a comprehensive end-to-end (E2E) test suite using Playwright to verify critical user flows in the ChessScribe application. The test suite will run locally to ensure that user flows remain functional when new features are implemented, preventing regressions and maintaining application quality.

## Plan Overview

The system should:
1. Set up Playwright testing framework in the frontend project (chess-scribe-convert)
2. Create E2E tests for critical user flows: authentication, image upload, PGN processing, and export functionality
3. Configure test environment to run against local development server
4. Implement test fixtures and helpers for common operations (authentication, API mocking)
5. Set up CI/CD integration (optional, for future) and local test execution scripts
6. Document test execution and maintenance procedures

## Implementation Plan

### Phase 1: Playwright Setup and Configuration

**Agent should:**
- Install Playwright and required dependencies in `chess-scribe-convert` project
- Create Playwright configuration file (`playwright.config.ts`)
- Configure test environment:
  - Base URL for local development server (http://localhost:8080)
  - Browser configurations (Chromium, Firefox, WebKit)
  - Test timeout settings
  - Screenshot and video capture on failure
  - Test data directory structure
- Set up test directory structure:
  - `tests/e2e/` for E2E tests
  - `tests/fixtures/` for test fixtures and helpers
  - `tests/utils/` for utility functions
- Add npm scripts for test execution:
  - `npm run test:e2e` - Run all E2E tests
  - `npm run test:e2e:ui` - Run tests with Playwright UI mode
  - `npm run test:e2e:debug` - Run tests in debug mode
  - `npm run test:e2e:headed` - Run tests in headed mode (visible browser)

**Key Integration Points:**
- Integrate with existing Vite/React project structure
- Ensure compatibility with existing development workflow
- Configure environment variables for test execution

**Deliverables:**
- Playwright installed and configured
- `playwright.config.ts` configuration file
- Test directory structure created
- npm scripts for test execution
- `.gitignore` updates for test artifacts

### Phase 2: Test Fixtures and Helpers

**Agent should:**
- Create authentication helper:
  - Mock Google Sign-In flow
  - Create authenticated user session
  - Handle token management for tests
  - Support different user roles if needed
- Create API mocking utilities:
  - Mock backend API responses for image processing
  - Mock authentication endpoints
  - Create reusable mock data fixtures
- Create page object models (POM) for key pages:
  - `SignInPage` - Sign-in page interactions
  - `IndexPage` - Main upload page interactions
  - `NotationDisplayPage` - PGN display and editing interactions
  - `ProfilePage` - User profile page interactions
- Create utility functions:
  - File upload helpers
  - Wait for API calls to complete
  - Screenshot capture utilities
  - Test data generators

**Key Integration Points:**
- Use Playwright's built-in request interception for API mocking
- Integrate with existing component structure
- Follow Page Object Model pattern for maintainability

**Deliverables:**
- Authentication test helpers
- API mocking utilities
- Page Object Models for key pages
- Utility functions for common test operations

### Phase 3: Core User Flow Tests

**Agent should:**

#### 3.1 Authentication Flow Tests
- Test Google Sign-In flow:
  - Verify sign-in page loads correctly
  - Test "Continue with Google" button functionality
  - Verify redirect to main page after successful sign-in
  - Test session persistence (refresh page, user still signed in)
- Test protected routes:
  - Verify unauthenticated users are redirected to sign-in
  - Verify authenticated users can access protected routes
  - Test sign-out functionality
  - Verify redirect after sign-out

#### 3.2 Image Upload and Processing Tests
- Test image upload flow:
  - Verify file selection dialog works
  - Test drag-and-drop functionality
  - Test image preview display
  - Test language selection dropdown
  - Test auto-crop toggle functionality
  - Test player metadata input (if implemented)
- Test image processing:
  - Verify processing starts after image upload
  - Test loading states during processing
  - Verify processed results are displayed
  - Test error handling for invalid images
  - Test error handling for API failures

#### 3.3 PGN Display and Editing Tests
- Test PGN notation display:
  - Verify PGN content is displayed correctly
  - Test move table rendering
  - Verify chess board displays correctly
  - Test move navigation (forward/backward)
  - Test move validation indicators
- Test PGN editing:
  - Test inline move editing
  - Verify edited moves are saved (if auto-save implemented)
  - Test move deletion
  - Test move addition
  - Verify board updates when moves are edited

#### 3.4 Export Functionality Tests
- Test Lichess export:
  - Verify "Open in Lichess" button functionality
  - Test PGN is correctly formatted for Lichess
  - Verify external link opens in new tab
  - Test processing completion flag (if implemented)
- Test Chess.com export:
  - Verify "Open in Chess.com" button functionality
  - Test PGN is correctly formatted for Chess.com
  - Verify external link opens in new tab

**Key Integration Points:**
- Use Page Object Models for test organization
- Mock backend API responses for consistent testing
- Test both happy paths and error scenarios

**Deliverables:**
- Authentication flow test suite
- Image upload and processing test suite
- PGN display and editing test suite
- Export functionality test suite

### Phase 4: Advanced Test Scenarios

**Agent should:**

#### 4.1 Error Handling Tests
- Test network error scenarios:
  - API timeout handling
  - Network disconnection
  - Server error responses (500, 503)
- Test validation error scenarios:
  - Invalid image format
  - Image too large
  - Unsupported language selection
  - Invalid PGN content

#### 4.2 Responsive Design Tests
- Test mobile viewport (375px, 414px):
  - Verify layout adapts correctly
  - Test touch interactions
  - Verify mobile navigation
- Test tablet viewport (768px, 1024px):
  - Verify layout adapts correctly
  - Test responsive breakpoints
- Test desktop viewport (1280px, 1920px):
  - Verify full layout displays correctly

#### 4.3 Cross-Browser Tests
- Test in Chromium (Chrome/Edge)
- Test in Firefox
- Test in WebKit (Safari)
- Verify consistent behavior across browsers

#### 4.4 Performance Tests
- Test page load times
- Test image processing response times
- Test large file handling
- Test concurrent user scenarios (if applicable)

**Key Integration Points:**
- Use Playwright's viewport and device emulation
- Leverage Playwright's network interception for error scenarios
- Use Playwright's performance API for performance testing

**Deliverables:**
- Error handling test suite
- Responsive design test suite
- Cross-browser test suite
- Performance test suite

### Phase 5: Test Documentation and Maintenance

**Agent should:**
- Create test documentation:
  - README for test suite (`tests/README.md`)
  - Test execution guide
  - Test maintenance guide
  - Troubleshooting guide
- Set up test reporting:
  - HTML test reports
  - Screenshot and video artifacts on failure
  - Test coverage reporting (if applicable)
- Create test data management:
  - Sample test images
  - Mock API response files
  - Test user credentials (mock)
- Document CI/CD integration (for future):
  - GitHub Actions workflow example
  - Test execution in CI environment
  - Test result reporting

**Key Integration Points:**
- Integrate with existing project documentation
- Follow project documentation standards
- Ensure test artifacts are properly organized

**Deliverables:**
- Test documentation
- Test reporting configuration
- Test data management setup
- CI/CD integration documentation

## Technical Specifications

### Playwright Configuration
```typescript
// playwright.config.ts
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost:8080',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:8080',
    reuseExistingServer: !process.env.CI,
  },
});
```

### Page Object Model Example
```typescript
// tests/fixtures/pages/SignInPage.ts
import { Page, Locator } from '@playwright/test';

export class SignInPage {
  readonly page: Page;
  readonly googleSignInButton: Locator;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;

  constructor(page: Page) {
    this.page = page;
    this.googleSignInButton = page.getByRole('button', { name: /continue with google/i });
  }

  async goto() {
    await this.page.goto('/signin');
  }

  async signInWithGoogle() {
    await this.googleSignInButton.click();
    // Handle Google OAuth flow (mock or real)
  }

  async isSignedIn(): Promise<boolean> {
    // Check for authenticated user indicators
    return await this.page.locator('[data-testid="user-avatar"]').isVisible();
  }
}
```

### Test Example
```typescript
// tests/e2e/authentication.spec.ts
import { test, expect } from '@playwright/test';
import { SignInPage } from '../fixtures/pages/SignInPage';

test.describe('Authentication', () => {
  test('should sign in with Google', async ({ page }) => {
    const signInPage = new SignInPage(page);
    await signInPage.goto();
    await signInPage.signInWithGoogle();
    await expect(page).toHaveURL('/');
    await expect(signInPage.isSignedIn()).resolves.toBe(true);
  });

  test('should redirect unauthenticated users to sign-in', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL('/signin');
  });
});
```

### API Mocking Example
```typescript
// tests/fixtures/api-mocks.ts
import { Page } from '@playwright/test';

export async function mockImageProcessing(page: Page, mockResponse: any) {
  await page.route('**/api/game/upload', async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockResponse),
    });
  });
}
```

### Test Directory Structure
```
chess-scribe-convert/
├── tests/
│   ├── e2e/
│   │   ├── authentication.spec.ts
│   │   ├── image-upload.spec.ts
│   │   ├── pgn-display.spec.ts
│   │   ├── export.spec.ts
│   │   └── error-handling.spec.ts
│   ├── fixtures/
│   │   ├── pages/
│   │   │   ├── SignInPage.ts
│   │   │   ├── IndexPage.ts
│   │   │   ├── NotationDisplayPage.ts
│   │   │   └── ProfilePage.ts
│   │   ├── api-mocks.ts
│   │   └── auth-helpers.ts
│   ├── utils/
│   │   ├── test-data.ts
│   │   └── helpers.ts
│   └── README.md
├── playwright.config.ts
└── package.json
```

### Package.json Scripts
```json
{
  "scripts": {
    "test:e2e": "playwright test",
    "test:e2e:ui": "playwright test --ui",
    "test:e2e:debug": "playwright test --debug",
    "test:e2e:headed": "playwright test --headed",
    "test:e2e:report": "playwright show-report"
  },
  "devDependencies": {
    "@playwright/test": "^1.40.0"
  }
}
```

## Acceptance Criteria

### Backend
- [ ] N/A (E2E tests are frontend-focused, but may require backend API mocking)

### Frontend
- [ ] Playwright installed and configured in `chess-scribe-convert` project
- [ ] `playwright.config.ts` created with proper configuration
- [ ] Test directory structure created (`tests/e2e/`, `tests/fixtures/`, `tests/utils/`)
- [ ] npm scripts added for test execution
- [ ] Authentication flow tests implemented and passing
- [ ] Image upload and processing tests implemented and passing
- [ ] PGN display and editing tests implemented and passing
- [ ] Export functionality tests implemented and passing
- [ ] Error handling tests implemented
- [ ] Responsive design tests implemented
- [ ] Cross-browser tests implemented (Chromium, Firefox, WebKit)
- [ ] Page Object Models created for key pages
- [ ] API mocking utilities implemented
- [ ] Test helpers and utilities created
- [ ] Test documentation created (`tests/README.md`)
- [ ] Test reports generate correctly (HTML reports, screenshots, videos)
- [ ] Tests can run locally against development server
- [ ] All tests pass consistently

## Dependencies

### Backend
- Existing API endpoints (for mocking and testing)
- Backend API running locally (for integration tests)

### Frontend
- Existing React/Vite application
- Existing component structure
- Existing routing configuration
- Existing authentication flow
- Development server (npm run dev)

## Impact Assessment

**Impact Level**: High

**Impact Description**: 
This feature significantly improves code quality and prevents regressions by providing automated end-to-end testing of critical user flows. It enables developers to verify that new features don't break existing functionality, reducing manual testing effort and catching bugs early in the development cycle. The test suite serves as living documentation of how the application should work and provides confidence when refactoring or adding new features. This is especially important as the application grows and becomes more complex.

## Effort Estimation

**Effort Level**: 6

**Effort Breakdown**:

### Backend
- N/A (E2E tests are frontend-focused)

### Frontend
- Playwright setup and configuration: 2 hours
- Test fixtures and helpers creation: 4 hours
- Page Object Models implementation: 3 hours
- Authentication flow tests: 2 hours
- Image upload and processing tests: 4 hours
- PGN display and editing tests: 4 hours
- Export functionality tests: 2 hours
- Error handling tests: 2 hours
- Responsive design tests: 2 hours
- Cross-browser tests: 2 hours
- Test documentation: 2 hours
- Test reporting setup: 1 hour
- Testing and refinement: 3 hours
- **Frontend Total**: 33 hours

**Total Estimated**: 33 hours

## Future Enhancements

This feature establishes the foundation for comprehensive E2E testing. Future enhancements may include:
- CI/CD integration (GitHub Actions, automated test runs on PRs)
- Visual regression testing (screenshot comparison)
- Performance benchmarking and monitoring
- Accessibility testing integration
- Test coverage reporting and metrics
- Parallel test execution optimization
- Test data management system
- Integration with test management tools
- Mobile device testing (real devices or emulators)
- Load testing for API endpoints
- Security testing scenarios

