# Evaluation Runner

A console application that runs batch evaluations on chess game images and generates an HTML report with benchmark results.

## Overview

This tool:
1. Scans the `EvaluationExamples` folder for language-specific game folders
2. Runs evaluation API calls for each game (image + ground truth text)
3. Collects all results and generates a comprehensive HTML report

## Prerequisites

- .NET 9.0 SDK
- The Chess Decoder API must be running on `http://localhost:5100`

## Usage

### Option 1: Using the shell script (recommended)

```bash
cd EvaluationRunner
./run-evaluation.sh
```

### Option 2: Manual build and run

```bash
cd EvaluationRunner
dotnet build -c Release
dotnet run -c Release -- [path-to-EvaluationExamples]
```

If no path is provided, it will default to `../Tests/data/EvaluationExamples` relative to the EvaluationRunner directory.

## Configuration

The evaluation uses the following default parameters (as specified in the curl example):
- `NumberOfColumns`: 6
- `Autocrop`: false
- `ApiBaseUrl`: http://localhost:5100

To change these, edit the constants in `Program.cs`.

## Output

The tool generates an `evaluation-report.html` file in the EvaluationRunner directory containing:

- **Global Summary**: Overall statistics including total tests, success rate, average scores, etc.
- **Results by Language**: Tables showing results grouped by language (English, Greek, etc.)
- **Detailed Results**: Move-by-move comparison for each game, including:
  - Ground truth moves vs extracted moves
  - Generated PGN
  - All metrics (normalized score, exact match, positional accuracy, etc.)

## Report Features

- Color-coded scores (green for high, orange for medium, red for low)
- Success/failure indicators
- Move comparison with visual highlighting
- Processing time metrics
- Language-specific grouping

