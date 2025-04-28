package pgn

import (
	"fmt"
	"os"
	"strings"
)

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

// ConvertMovesToPGN converts a slice of chess moves into PGN format and returns the PGN string.
func ConvertMovesToPGN(moves []string) string {
	var pgnBuilder strings.Builder

	// Write the PGN headers.
	pgnBuilder.WriteString("[Event \"Unknown Event\"]\n")
	pgnBuilder.WriteString("[Site \"Unknown Site\"]\n")
	pgnBuilder.WriteString("[Date \"????.??.??\"]\n")
	pgnBuilder.WriteString("[Round \"?\"]\n")
	pgnBuilder.WriteString("[White \"Unknown\"]\n")
	pgnBuilder.WriteString("[Black \"Unknown\"]\n")
	pgnBuilder.WriteString("[Result \"*\"]\n\n")

	// Write the moves.
	for i, move := range moves {
		if i%2 == 0 {
			pgnBuilder.WriteString(fmt.Sprintf("%d. ", (i/2)+1))
		}
		pgnBuilder.WriteString(fmt.Sprintf("%s ", move))
		if i%2 == 1 {
			pgnBuilder.WriteString("\n")
		}
	}

	pgnBuilder.WriteString("\n*")
	return pgnBuilder.String()
}