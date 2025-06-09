# Chess Decoder Image Processing Evaluation System

This evaluation system allows you to test the `ProcessImageAsync` function against ground truth data to measure its accuracy and performance. The system provides normalized scoring where **0 is the perfect score** (minimum distance from ground truth).

## Features

- **API Endpoint**: RESTful endpoint for evaluating chess image processing accuracy
- **Multiple Metrics**: Exact match, positional accuracy, Levenshtein distance, and longest common subsequence
- **Normalized Scoring**: Single score (0-1) where 0 is perfect and 1 is worst
- **Detailed JSON Response**: Complete evaluation results including move-by-move comparison
- **File Upload Support**: Direct image and ground truth file upload via multipart form data

## API Endpoint

### POST `/ChessDecoder/evaluate`

Evaluates a chess image against ground truth data and returns comprehensive accuracy metrics.

**Request Parameters:**
- `image` (required): Chess image file (JPG, PNG, etc.)
- `groundTruth` (required): Ground truth file (.txt or .pgn)
- `language` (optional): Chess notation language (default: "English")

**Response Format:**
```json
{
  "imageFileName": "chess-game.jpg",
  "groundTruthFileName": "game1.txt",
  "language": "English",
  "isSuccessful": true,
  "errorMessage": "",
  "processingTimeSeconds": 2.341,
  "metrics": {
    "normalizedScore": 0.127,
    "exactMatchScore": 0.897,
    "positionalAccuracy": 0.931,
    "levenshteinDistance": 4,
    "longestCommonSubsequence": 52
  },
  "moveCounts": {
    "groundTruthMoves": 58,
    "extractedMoves": 56
  },
  "moves": {
    "groundTruth": ["e4", "e5", "Nf3", "Nc6", "..."],
    "extracted": ["e4", "e5", "Nf3", "Nc6", "..."]
  },
  "generatedPgn": "[Event \"??\"]\n[Site \"??\"]\n...\n1. e4 e5 2. Nf3 Nc6 *"
}
```

## Getting Started

### Prerequisites

1. Set your OpenAI API key as an environment variable:
   ```bash
   export OPENAI_API_KEY="your-api-key-here"
   ```

2. Start the Chess Decoder API:
   ```bash
   dotnet run
   ```

3. Prepare test data:
   - Chess images (JPG, PNG, etc.)
   - Corresponding ground truth PGN files (like `Game1.txt`)

### Usage Examples

#### 1. Using cURL

```bash
# Basic evaluation
curl -X POST "https://localhost:7000/ChessDecoder/evaluate" \
  -H "Content-Type: multipart/form-data" \
  -F "image=@chess-image.jpg" \
  -F "groundTruth=@Tests/data/GroundTruth/Game1.txt"

# With Greek language support
curl -X POST "https://localhost:7000/ChessDecoder/evaluate" \
  -H "Content-Type: multipart/form-data" \
  -F "image=@greek-chess.jpg" \
  -F "groundTruth=@greek-game.txt" \
  -F "language=Greek"
```

#### 2. Using JavaScript/Fetch

```javascript
async function evaluateChessImage(imageFile, groundTruthFile, language = 'English') {
  const formData = new FormData();
  formData.append('image', imageFile);
  formData.append('groundTruth', groundTruthFile);
  formData.append('language', language);

  const response = await fetch('/ChessDecoder/evaluate', {
    method: 'POST',
    body: formData
  });

  const result = await response.json();
  return result;
}

// Usage
const imageFile = document.getElementById('imageInput').files[0];
const groundTruthFile = document.getElementById('groundTruthInput').files[0];
const evaluation = await evaluateChessImage(imageFile, groundTruthFile);
console.log('Normalized Score:', evaluation.metrics.normalizedScore);
```

#### 3. Using Python

```python
import requests

def evaluate_chess_image(image_path, ground_truth_path, language='English'):
    url = 'https://localhost:7000/ChessDecoder/evaluate'
    
    with open(image_path, 'rb') as img, open(ground_truth_path, 'rb') as gt:
        files = {
            'image': img,
            'groundTruth': gt
        }
        data = {
            'language': language
        }
        
        response = requests.post(url, files=files, data=data)
        return response.json()

# Usage
result = evaluate_chess_image('chess.jpg', 'game1.txt')
print(f"Normalized Score: {result['metrics']['normalizedScore']}")
print(f"Exact Match Score: {result['metrics']['exactMatchScore']}")
```

## Evaluation Metrics

### Primary Metrics

1. **Normalized Score** (0-1, lower is better)
   - Weighted combination of all metrics
   - 0 = perfect match, 1 = worst possible

2. **Exact Match Score** (0-1, higher is better)
   - Percentage of moves that match exactly at the same position

3. **Positional Accuracy** (0-1, higher is better)
   - Accuracy considering move order and position

4. **Levenshtein Distance** (integer, lower is better)
   - Edit distance between move sequences

5. **Longest Common Subsequence** (integer, higher is better)
   - Length of longest matching subsequence

### Scoring Formula

The normalized score uses weighted components:
- Exact Match: 40%
- Positional Accuracy: 30%
- Levenshtein Distance: 20%
- LCS Component: 10%

## Programmatic Testing

### 1. Development Testing (Mocked)

```csharp
// For unit tests - no API calls
var evaluationService = new ImageProcessingEvaluationService(
    mockImageService, 
    logger, 
    useRealApi: false); // Disabled - will throw exception

// Or with mocked results
mockImageService
    .Setup(x => x.ExtractMovesFromImageToStringAsync(It.IsAny<string>(), It.IsAny<string>()))
    .ReturnsAsync(new string[] { "e4", "e5", "Nf3", "Nc6" });
```

### 2. Integration Testing (Real API)

```csharp
// For real evaluations - uses actual OpenAI API
var evaluationService = new ImageProcessingEvaluationService(
    realImageService, 
    logger, 
    useRealApi: true); // Enabled - will make API calls
```

### 3. Controlled Evaluation Tests

The test suite includes several categories:

- **Skipped by Default**: Real API tests are marked with `[Fact(Skip = "...")]`
- **Mock Tests**: Test the evaluation logic without API calls
- **Controlled Tests**: Use mocked services to test specific scenarios

## File Organization

```
Tests/
├── Services/
│   ├── ImageProcessingEvaluationService.cs    # Main evaluation service
│   ├── ImageProcessingEvaluationTests.cs      # Unit tests
│   └── ImageProcessingServiceTests.cs         # Original service tests
├── data/
│   └── GroundTruth/
│       └── Game1.txt                          # Sample ground truth data
└── README_Evaluation.md                       # This file

Controllers/
└── ChessDecoderController.cs                  # API endpoints including /evaluate
```

## Ground Truth Format

Ground truth files should be in PGN format or simple move list format:

```
1. e4 e5 
2. Nf3 Nc6 
3. Bb5 Bb4 
4. c3 Ba5 
*
```

The evaluation system will parse both full PGN files and simple move lists.

## Example Response

```json
{
  "imageFileName": "test-chess.jpg",
  "groundTruthFileName": "Game1.txt",
  "language": "English",
  "isSuccessful": true,
  "errorMessage": "",
  "processingTimeSeconds": 2.34,
  "metrics": {
    "normalizedScore": 0.127,
    "exactMatchScore": 0.897,
    "positionalAccuracy": 0.931,
    "levenshteinDistance": 4,
    "longestCommonSubsequence": 52
  },
  "moveCounts": {
    "groundTruthMoves": 58,
    "extractedMoves": 56
  },
  "moves": {
    "groundTruth": ["e4", "e5", "Nf3", "Nc6", "Bb5", "..."],
    "extracted": ["e4", "e5", "Nf3", "Nc6", "Ba4", "..."]
  },
  "generatedPgn": "[Event \"??\"]\n[Site \"??\"]\n[Date \"??\"]\n[Round \"??\"]\n[White \"??\"]\n[Black \"??\"]\n[Result \"*\"]\n\n1. e4 e5 2. Nf3 Nc6 3. Ba4 *"
}
```

## Language Support

The evaluation system supports different chess notation languages:
- **English**: Standard algebraic notation (e4, Nf3, etc.)
- **Greek**: Greek chess notation (ε4, Ιf3, etc.)
- Additional languages can be added by extending the character mapping

## Error Handling

The API returns appropriate HTTP status codes:

- **200 OK**: Successful evaluation
- **400 Bad Request**: Missing or invalid files
- **401 Unauthorized**: Invalid or missing OpenAI API key
- **500 Internal Server Error**: Processing errors

Error responses include details:
```json
{
  "status": 400,
  "message": "No image file provided"
}
```

## Cost Considerations

Each evaluation makes a real OpenAI API call, which incurs costs. Use the evaluation system judiciously:

- Test with a few images first to understand accuracy
- Monitor your API usage and costs
- Consider batching evaluations for efficiency
- Use the development/mock tests for initial development

## Security Notes

- The evaluation endpoint requires both image and ground truth files
- Temporary files are automatically cleaned up after processing
- No persistent storage of uploaded files
- OpenAI API key validation occurs before processing

## Performance Tips

- Use appropriately sized images (the system resizes to 1024x1024 max)
- Ground truth files should be reasonably sized
- Consider image quality and chess notation clarity for better results
- Network latency affects processing time for API calls

## Extending the System

### Adding New Metrics

1. Add metric calculation in `ImageProcessingEvaluationService`
2. Update `EvaluationResult` class with new property
3. Modify `ComputeNormalizedScore` to include new metric
4. Update API response format to include new metric

### Adding New Languages

1. Extend character mapping in `ImageProcessingService`
2. Add language-specific test cases
3. Update evaluation tests for new language support

### Custom Scoring

You can implement custom scoring by:
1. Inheriting from `ImageProcessingEvaluationService`
2. Overriding `ComputeNormalizedScore` method
3. Adjusting weights or adding new components
4. Creating a custom evaluation endpoint 