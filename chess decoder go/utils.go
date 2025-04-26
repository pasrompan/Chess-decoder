package main

import (
	"bufio"
	"os"
	"strings"
)

// GreekToEnglishMap maps Greek chess notation to English.

// TranslateMoves translates a slice of moves from Greek to English notation.
func TranslateMoves(moves []string) []string {
	translatedMoves := make([]string, len(moves))
	for i, move := range moves {
		translatedMove := move
		for greek, english := range GreekToEnglishMap {
			translatedMove = strings.ReplaceAll(translatedMove, greek, english)
		}
		translatedMoves[i] = translatedMove
	}
	return translatedMoves
}

// ReadFileLines reads a file and returns a slice of its lines.
func ReadFileLines(filePath string) ([]string, error) {
	file, err := os.Open(filePath)
	if err != nil {
		return nil, err
	}
	defer file.Close()

	var lines []string
	scanner := bufio.NewScanner(file)
	for scanner.Scan() {
		lines = append(lines, scanner.Text())
	}
	return lines, scanner.Err()
}

// WriteLinesToFile writes a slice of lines to a file.
func WriteLinesToFile(lines []string, filePath string) error {
	file, err := os.Create(filePath)
	if err != nil {
		return err
	}
	defer file.Close()

	w := bufio.NewWriter(file)
	for _, line := range lines {
		_, err := w.WriteString(line + "\n")
		if err != nil {
			return err
		}
	}
	return w.Flush()
}
