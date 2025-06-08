# Chess Decoder Image Processing Evaluation System

This evaluation system allows you to test the `ProcessImageAsync` function against ground truth data to measure its accuracy and performance. The system provides normalized scoring where **0 is the perfect score** (minimum distance from ground truth).

## Features

- **Controlled API Usage**: Flag-controlled system to prevent accidental OpenAI API calls
- **Multiple Metrics**: Exact match, positional accuracy, Levenshtein distance, and longest common subsequence
- **Normalized Scoring**: Single score (0-1) where 0 is perfect and 1 is worst
- **Batch Processing**: Evaluate multiple test cases and get aggregate results
- **Detailed Reporting**: Move-by-move comparison and detailed metrics

## Getting Started

### Prerequisites

1. Set your OpenAI API key as an environment variable:
   ```bash
   export OPENAI_API_KEY="your-api-key-here"
   ```

2. Prepare test data:
   - Chess images (JPG, PNG, etc.)
   - Corresponding ground truth PGN files (like `Game1.txt`)

### Basic Usage

#### 1. Single Evaluation

```csharp
// Create the evaluation service
var evaluationService = new ImageProcessingEvaluationService(
    imageProcessingService, 
    logger, 
    useRealApi: true); // Enable real API calls

// Run evaluation
var result = await evaluationService.EvaluateAsync(
    "path/to/chess-image.jpg", 
    "Tests/data/GroundTruth/Game1.txt");

// Display results
result.PrintSummary();
```

#### 2. Multiple Evaluations

```csharp
var testCases = new List<TestCase>
{
    new TestCase 
    { 
        ImagePath = "test1.jpg", 
        GroundTruthPath = "ground1.txt",
        Language = "English"
    },
    new TestCase 
    { 
        ImagePath = "test2.jpg", 
        GroundTruthPath = "ground2.txt",
        Language = "Greek"
    }
};

var aggregateResult = await evaluationService.EvaluateMultipleAsync(testCases);
aggregateResult.PrintSummary();
```

#### 3. Console Application

Use the provided console application for command-line evaluation:

```bash
# Single evaluation
dotnet run --project Tests -- --image "chess1.jpg" --groundtruth "Game1.txt"

# With Greek language support
dotnet run --project Tests -- -i "greek_chess.jpg" -g "Greek1.txt" -l "Greek"
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

## Testing Approaches

### 1. Development Testing (Mocked)

```csharp
// For unit tests - no API calls
var evaluationService = new ImageProcessingEvaluationService(
    mockImageService, 
    logger, 
    useRealApi: false); // Disabled - will throw exception

// Or with mocked results
mockImageService
    .Setup(x => x.ProcessImageAsync(It.IsAny<string>(), It.IsAny<string>()))
    .ReturnsAsync(knownPgnResult);
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
├── EvaluationRunner.cs                        # Console application
└── README_Evaluation.md                       # This file
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

## Example Output

```
=== Evaluation Results ===
Image: test-chess.jpg
Ground Truth: Game1.txt
Language: English
Success: True
Processing Time: 2.34s
Ground Truth Moves: 58
Extracted Moves: 56
Normalized Score: 0.127 (0 = perfect)
Exact Match Score: 0.897
Positional Accuracy: 0.931
Levenshtein Distance: 4
Longest Common Subsequence: 52
```

## Language Support

The evaluation system supports different chess notation languages:
- **English**: Standard algebraic notation (e4, Nf3, etc.)
- **Greek**: Greek chess notation (ε4, Ιf3, etc.)
- Additional languages can be added by extending the character mapping

## Safety Features

- **API Key Validation**: Checks for OpenAI API key before starting
- **File Validation**: Verifies image and ground truth files exist
- **Flag Protection**: `useRealApi` flag prevents accidental API usage
- **Error Handling**: Comprehensive error handling and logging
- **Skipped Tests**: Integration tests are skipped by default

## Cost Considerations

Each evaluation makes a real OpenAI API call, which incurs costs. Use the evaluation system judiciously:

- Start with mock tests for development
- Use single evaluations for debugging
- Run batch evaluations only when needed
- Monitor your API usage and costs

## Extending the System

### Adding New Metrics

1. Add metric calculation in `ImageProcessingEvaluationService`
2. Update `EvaluationResult` class with new property
3. Modify `ComputeNormalizedScore` to include new metric
4. Update display methods to show new metric

### Adding New Languages

1. Extend character mapping in `ImageProcessingService`
2. Add language-specific test cases
3. Update evaluation tests for new language support

### Custom Scoring

You can implement custom scoring by:
1. Inheriting from `ImageProcessingEvaluationService`
2. Overriding `ComputeNormalizedScore` method
3. Adjusting weights or adding new components 