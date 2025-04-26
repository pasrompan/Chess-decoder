package main

import (
	"fmt"
	_ "image/jpeg"
	_ "image/png"
	"log"
	"os"
	"strings"
)

func main() {

	imagePath := "data/IMG_4585.png"
	apiKey := "dummy_api_key" // os.Args[2]

	// Load the image
	img, err := LoadImage(imagePath)
	if err != nil {
		log.Fatalf("Failed to load image: %s", err)
	}

	// Resize the image if necessary
	img = ResizeImage(img, 1024, 1024)

	// Convert the image to bytes
	imageBytes, err := ImageToBytes(img)
	if err != nil {
		log.Fatalf("Failed to convert image to bytes: %s", err)
	}

	// Extract text from the image using OpenAI
	text, err := ExtractTextFromImage(apiKey, imageBytes)
	if err != nil {
		log.Fatalf("Failed to extract text from image: %s", err)
	}

	fmt.Println("Extracted Text:")
	fmt.Println(text)

	// Convert the Greek chess moves to English
	englishMoves, err := ConvertGreekMovesToEnglish(strings.Split(text, "\n"))
	if err != nil {
		log.Fatalf("Failed to convert Greek moves to English: %s", err)
	}

	// Define the output file path
	outputFilePath := "output.pgn"

	WritePGNFile(englishMoves, outputFilePath)
	if err != nil {
		log.Fatalf("Failed to generate PGN content: %s", err)
	}

	fmt.Printf("The chess game has been successfully converted and saved to %s\n", outputFilePath)
}

// GreekToEnglishMap maps Greek chess piece names to their English counterparts.
var GreekToEnglishMap = map[string]string{
	"Π": "R", // Πύργος (Rook)
	"Α": "B", // Αλογο (Knight)
	"Β": "Q", // Βασίλισσα (Queen)
	"Ι": "N", // Ιππος (Knight)
	"Ρ": "K", // Ρήγας (King)
	"0": "0", // Castling short
	"O": "0", // Castling short
	"x": "x", // Capture
	"+": "+", // Check
	"#": "#", // Checkmate
	"α": "a",
	"β": "b",
	"γ": "c",
	"δ": "d",
	"ε": "e",
	"ζ": "f",
	"η": "g",
	"θ": "h",
}

// ConvertGreekMovesToEnglish takes a slice of chess moves in Greek and converts them to English.
func ConvertGreekMovesToEnglish(greekMoves []string) ([]string, error) {
	englishMoves := make([]string, len(greekMoves))

	for i, move := range greekMoves {
		englishMove := move

		// Replace Greek piece names with English equivalents.
		for greek, english := range GreekToEnglishMap {
			englishMove = strings.ReplaceAll(englishMove, greek, english)
		}

		// Handle special cases and notation differences if necessary.
		// Example: Castling, pawn promotion, etc.

		englishMoves[i] = englishMove
	}
	return englishMoves, nil
}

// WritePGNFile takes a slice of chess moves in English notation and writes them to a file in PGN format.
func WritePGNFile(moves []string, outputPath string) error {
	// Open the file for writing, create it if it does not exist, truncate it if it does.
	file, err := os.Create(outputPath)
	if err != nil {
		return err
	}
	defer file.Close()

	// Write the PGN headers. These are placeholders and might need to be adjusted based on actual game data.
	_, err = file.WriteString("[Event \"Unknown Event\"]\n")
	if err != nil {
		return err
	}
	_, err = file.WriteString("[Site \"Unknown Site\"]\n")
	if err != nil {
		return err
	}
	_, err = file.WriteString("[Date \"????.??.??\"]\n")
	if err != nil {
		return err
	}
	_, err = file.WriteString("[Round \"?\"]\n")
	if err != nil {
		return err
	}
	_, err = file.WriteString("[White \"Unknown\"]\n")
	if err != nil {
		return err
	}
	_, err = file.WriteString("[Black \"Unknown\"]\n")
	if err != nil {
		return err
	}
	_, err = file.WriteString("[Result \"*\"]\n\n")
	if err != nil {
		return err
	}

	// Write the moves to the file in PGN format.
	for i, move := range moves {
		// PGN format requires move numbers before each White's move.
		if i%2 == 0 {
			_, err = file.WriteString(fmt.Sprintf("%d. ", (i/2)+1))
			if err != nil {
				return err
			}
		}

		// Write the move itself.
		_, err = file.WriteString(fmt.Sprintf("%s ", move))
		if err != nil {
			return err
		}

		// Add a newline every full move (after Black's move) for readability.
		if i%2 == 1 {
			_, err = file.WriteString("\n")
			if err != nil {
				return err
			}
		}
	}

	// Add the result at the end of the file. Placeholder as the actual result might need to be determined.
	_, err = file.WriteString("\n*")
	if err != nil {
		return err
	}

	return nil
}

// Helper function to format moves into a more PGN friendly format.
// This might include adding '=' for pawn promotions, '#' for checkmates, etc.
// Currently, it's a placeholder that simply returns the input moves.
func formatMovesForPGN(moves []string) []string {
	formattedMoves := make([]string, len(moves))
	for i, move := range moves {
		// Example transformation: If a move ends with "Q", it might be a pawn promotion. This is oversimplified.
		// Real implementation would need to parse the move and apply the correct transformation.
		if strings.HasSuffix(move, "Q") {
			formattedMoves[i] = move + "="
		} else {
			formattedMoves[i] = move
		}
	}
	return formattedMoves
}
