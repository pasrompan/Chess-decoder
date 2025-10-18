# API Migration Guide

## Overview

Both **old routes** and **new routes** are now available to allow zero-downtime migration. The legacy `ChessDecoderController` has been recreated as a compatibility layer that redirects to the new controllers.

⚠️ **Legacy routes log warnings** - Monitor your logs to track migration progress.

## Migration Strategy

1. ✅ **Backend deployed** - Both old and new routes work
2. 🔄 **Frontend migration** - Update frontend to use new routes gradually
3. 🗑️ **Cleanup** - Remove `LegacyChessDecoderController.cs` once frontend is migrated

## Available Routes

### Health Check

| Old Route | New Route | Status |
|-----------|-----------|--------|
| `GET /ChessDecoder/health` | `GET /api/game/health` | ✅ Both work |

**Test:**
```bash
# Old (works with warnings)
curl http://localhost:5100/ChessDecoder/health

# New (recommended)
curl http://localhost:5100/api/game/health
```

---

### Upload & Process Image

| Old Route | New Route | Status |
|-----------|-----------|--------|
| `POST /ChessDecoder/upload/v2` | `POST /api/game/upload` | ✅ Both work |

**Test:**
```bash
# Old (works with warnings)
curl -X POST http://localhost:5100/ChessDecoder/upload/v2 \
  -F "image=@test.jpg" \
  -F "userId=user123" \
  -F "language=English" \
  -F "autoCrop=false"

# New (recommended)
curl -X POST http://localhost:5100/api/game/upload \
  -F "image=@test.jpg" \
  -F "userId=user123" \
  -F "language=English" \
  -F "autoCrop=false"
```

**Request Format (unchanged):**
```
Content-Type: multipart/form-data

Form Data:
- image: [file] (required)
- userId: [string] (required)
- language: [string] (optional, default: "English")
- autoCrop: [boolean] (optional, default: false)
```

---

### Mock Upload (Testing)

| Old Route | New Route | Status |
|-----------|-----------|--------|
| `POST /ChessDecoder/mockupload` | `POST /api/mock/upload` | ✅ Both work |

**Test:**
```bash
# Old (works with warnings)
curl -X POST http://localhost:5100/ChessDecoder/mockupload \
  -F "image=@test.jpg" \
  -F "language=English" \
  -F "autoCrop=false"

# New (recommended)
curl -X POST http://localhost:5100/api/mock/upload \
  -F "image=@test.jpg" \
  -F "language=English" \
  -F "autoCrop=false"
```

---

### Debug Endpoints

| Old Route | New Route | Status |
|-----------|-----------|--------|
| `POST /ChessDecoder/debug/upload` | `POST /api/debug/upload` | ✅ Both work |
| `POST /ChessDecoder/debug/split-columns` | `POST /api/debug/split-columns` | ✅ Both work |
| `POST /ChessDecoder/debug/image-with-boundaries` | `POST /api/debug/image-with-boundaries` | ✅ Both work |
| `POST /ChessDecoder/debug/table-boundaries` | `POST /api/debug/table-boundaries` | ✅ Both work |
| `POST /ChessDecoder/debug/crop-image` | `POST /api/debug/crop-image` | ✅ Both work |
| `POST /ChessDecoder/debug/table-analysis` | `POST /api/debug/table-analysis` | ✅ Both work |

---

### Evaluation

| Old Route | New Route | Status |
|-----------|-----------|--------|
| `POST /ChessDecoder/evaluate` | `POST /api/evaluation/evaluate` | ⚠️ Need to add legacy route |

**Note:** Evaluation endpoint still needs legacy compatibility route added if used by frontend.

---

## New Endpoints (No Legacy Equivalent)

These are brand new endpoints with no old routes:

| Route | Description |
|-------|-------------|
| `GET /api/game/{gameId}` | Get game by ID |
| `GET /api/game/user/{userId}` | List user's games (paginated) |
| `DELETE /api/game/{gameId}` | Delete a game |

---

## Frontend Migration Checklist

Track your migration progress:

### Core Features
- [ ] Health check endpoint
- [ ] Main upload endpoint (`/upload/v2` → `/api/game/upload`)
- [ ] Mock upload endpoint
- [ ] Error handling updated for new response format

### Debug Features (if used)
- [ ] Debug upload
- [ ] Debug split columns
- [ ] Debug image with boundaries
- [ ] Debug table boundaries
- [ ] Debug crop image
- [ ] Debug table analysis

### New Features to Integrate
- [ ] Get game by ID
- [ ] List user games with pagination
- [ ] Delete game functionality

---

## Monitoring Migration Progress

The legacy controller logs warnings for every old endpoint usage. Monitor your logs:

```bash
# Watch for legacy endpoint warnings
docker logs -f chess-decoder-api | grep "Legacy endpoint"

# or locally
dotnet run | grep "Legacy endpoint"
```

Example log output:
```
[Warning] Legacy endpoint /ChessDecoder/upload/v2 called. Please migrate to /api/game/upload
```

---

## When to Remove Legacy Controller

Remove `LegacyChessDecoderController.cs` when:

1. ✅ All frontend code updated to use new routes
2. ✅ No legacy endpoint warnings in logs for 7+ days
3. ✅ QA/staging environments fully tested
4. ✅ Rollback plan documented

**To remove:**
```bash
# Delete the legacy controller
rm Controllers/LegacyChessDecoderController.cs

# Update documentation
git commit -m "Remove legacy API compatibility layer"
```

---

## Response Format Changes

### Most responses remain the same
The response structure is unchanged for most endpoints. Key differences:

**Error responses** now use consistent format:
```json
{
  "message": "Error description"
}
```

**Upload responses** include new fields:
```json
{
  "gameId": "guid",
  "pgnContent": "...",
  "validation": {...},
  "processingTimeMs": 1234,
  "creditsRemaining": 95,
  "processedImageUrl": "data:image/png;base64,..."
}
```

---

## Testing Both Routes

You can use this script to verify both old and new routes work:

```bash
#!/bin/bash
# test-migration.sh

BASE_URL="http://localhost:5100"
TEST_IMAGE="test.jpg"
TEST_USER="test-user-123"

echo "Testing legacy routes..."
curl -X POST $BASE_URL/ChessDecoder/health
curl -X POST $BASE_URL/ChessDecoder/upload/v2 -F "image=@$TEST_IMAGE" -F "userId=$TEST_USER"

echo -e "\n\nTesting new routes..."
curl -X POST $BASE_URL/api/game/health
curl -X POST $BASE_URL/api/game/upload -F "image=@$TEST_IMAGE" -F "userId=$TEST_USER"

echo -e "\n\nDone! Check logs for warnings about legacy endpoints."
```

---

## Breaking Changes

**None!** All functionality preserved. Only route paths changed.

## Questions?

Contact the backend team or check:
- `REFACTORING_SUMMARY.md` - Detailed architecture changes
- `Controllers/LegacyChessDecoderController.cs` - Legacy implementation
- `Controllers/GameController.cs` - New implementation

---

**Last Updated:** October 18, 2025  
**Migration Deadline:** TBD (no rush, both routes work)  
**Support:** Monitor logs with `grep "Legacy endpoint"`

