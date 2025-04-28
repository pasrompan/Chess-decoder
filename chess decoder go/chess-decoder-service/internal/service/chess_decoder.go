package service

import (
	"fmt"
	"net/http"
	"os"
	"strings"

	"bytes"
	"chess-decoder-service/pkg/images"
	"context"
	"encoding/base64"
	"image"
	_ "image/jpeg"
	"image/png"
	_ "image/png"
	"time"

	"github.com/nfnt/resize"
	"github.com/sashabaranov/go-openai"
)

// ChessDecoderService handles the logic for processing chess images and generating PGN files.
type ChessDecoderService struct{}

// NewChessDecoderService creates a new instance of ChessDecoderService.
func NewChessDecoderService() *ChessDecoderService {
	return &ChessDecoderService{}
}

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

// ProcessImage handles the image upload, processes it, and returns the PGN file.
func (s *ChessDecoderService) ProcessImage(w http.ResponseWriter, r *http.Request) {
	// Parse the multipart form to retrieve the image file
	err := r.ParseMultipartForm(10 << 20) // Limit the size to 10 MB
	if err != nil {
		http.Error(w, "Unable to parse form", http.StatusBadRequest)
		return
	}

	file, _, err := r.FormFile("image")
	if err != nil {
		http.Error(w, "Unable to retrieve file", http.StatusBadRequest)
		return
	}
	defer file.Close()

	// Load the image
	img, _, err := image.Decode(file)
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to load image: %s", err), http.StatusInternalServerError)
		return
	}

	// Convert the image to bytes
	imageBytes, err := images.ImageToBytes(img)
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to convert image to bytes: %s", err), http.StatusInternalServerError)
		return
	}

	// Extract text from the image using OpenAI
	apiKey := os.Getenv("OPENAI_API_KEY")
	language := "English"
	text, err := ExtractTextFromImage(apiKey, imageBytes, language)
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to extract text from image: %s", err), http.StatusInternalServerError)
		return
	}

	// Convert the extracted text to PGN format
	var englishMoves []string
	if language == "Greek" {
		englishMoves, err = ConvertGreekMovesToEnglish(strings.Split(text, "\n"))
		if err != nil {
			http.Error(w, fmt.Sprintf("Failed to convert Greek moves to English: %s", err), http.StatusInternalServerError)
			return
		}
	} else {
		englishMoves = strings.Split(text, "\n")
	}

	// Generate the PGN content
	pgnContent, err := GeneratePGNContent(englishMoves)
	if err != nil {
		http.Error(w, fmt.Sprintf("Failed to generate PGN content: %s", err), http.StatusInternalServerError)
		return
	}

	// Set the response header to indicate a file download
	w.Header().Set("Content-Disposition", "attachment; filename=game.pgn")
	w.Header().Set("Content-Type", "application/octet-stream")
	w.WriteHeader(http.StatusOK)

	// Write the PGN content to the response
	if _, err := w.Write([]byte(pgnContent)); err != nil {
		http.Error(w, "Failed to write response", http.StatusInternalServerError)
		return
	}
}

// LoadImage loads an image file and returns its decoded image.Image object.
func LoadImage(filePath string) (image.Image, error) {
	file, err := os.Open(filePath)
	if err != nil {
		return nil, err
	}
	defer file.Close()

	img, _, err := image.Decode(file)
	if err != nil {
		return nil, err
	}

	return img, nil
}

// ResizeImage resizes the image to a maximum width and height while maintaining aspect ratio.
func ResizeImage(img image.Image, maxWidth, maxHeight uint) image.Image {
	return resize.Thumbnail(maxWidth, maxHeight, img, resize.Lanczos3)
}

// ImageToBytes converts an image.Image to a byte slice in PNG format.
func ImageToBytes(img image.Image) ([]byte, error) {
	var buf bytes.Buffer
	err := png.Encode(&buf, img)
	if err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
}

func ExtractTextFromImage(apiKey string, imageBytes []byte, language string) (string, error) {
	client := openai.NewClient(apiKey)

	base64Image := base64.StdEncoding.EncodeToString(imageBytes)

	ctx, cancel := context.WithTimeout(context.Background(), 2*time.Minute)
	defer cancel()

	// Get the valid Greek chess characters
	validChars := GetChessNotationCharacters(language)
	if len(validChars) == 0 {
		return "", nil // No valid characters found
	}

	// Build the prompt with the valid characters
	promptText := "You are an OCR engine. Transcribe all visible handwriting or printed text from this image exactly as it appears, but only include characters that are valid in a chess game. The valid characters are: "

	// Add each valid character to the prompt
	for i, char := range validChars {
		if i > 0 {
			promptText += ", "
		}
		promptText += char
	}

	promptText += ". Do not include any other characters, and preserve any misspellings, punctuation, or line breaks. Return only the raw text."

	resp, err := client.CreateChatCompletion(
		ctx,
		openai.ChatCompletionRequest{
			Model: openai.GPT4oLatest,
			Messages: []openai.ChatCompletionMessage{
				{
					Role: openai.ChatMessageRoleUser,
					MultiContent: []openai.ChatMessagePart{
						{
							Type: openai.ChatMessagePartTypeText,
							Text: promptText,
						},
						{
							Type: openai.ChatMessagePartTypeImageURL,
							ImageURL: &openai.ChatMessageImageURL{
								URL: "data:image/png;base64," + base64Image,
							},
						},
					},
				},
			},
			MaxTokens:   300,
			Temperature: 0,
		},
	)

	if err != nil {
		return "", err
	}

	if len(resp.Choices) > 0 {
		content := resp.Choices[0].Message.Content

		// Extract text between ``` and the end of content if no end marker
		startMarker := "```"

		startIndex := -1

		if startIdx := bytes.Index([]byte(content), []byte(startMarker)); startIdx != -1 {
			startIndex = startIdx + len(startMarker)

			// No end marker, return from start marker to end
			return content[startIndex:], nil
		}

		// Fall back to original content if no markers found
		return content, nil
	}

	return "", nil

}

// GetValidGreekChessCharacters returns a list of valid characters in a Greek-written chess game.
// GetChessNotationCharacters returns a list of valid characters in chess notation for the specified language.
func GetChessNotationCharacters(language string) []string {
	switch language {
	case "Greek":
		return []string{
			"Π", "Α", "Β", "Ι", "Ρ", // Greek piece names
			"0", "O", "x", "+", "#", // Special symbols
			"α", "β", "γ", "δ", "ε", "ζ", "η", "θ", // Greek file letters
			"1", "2", "3", "4", "5", "6", "7", "8", // Rank numbers
		}
	case "English":
		return []string{
			"R", "N", "B", "Q", "K", // English piece names
			"x", "+", "#", "0", "=", // Special symbols
			"a", "b", "c", "d", "e", "f", "g", "h", // File letters
			"1", "2", "3", "4", "5", "6", "7", "8", // Rank numbers
		}
	default:
		return []string{} // Return empty slice for unsupported languages
	}
}

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

// GeneratePGNContent takes a slice of chess moves in English notation and returns them as a string in PGN format.
func GeneratePGNContent(moves []string) (string, error) {
	var buffer strings.Builder

	// Write the PGN headers. These are placeholders and might need to be adjusted based on actual game data.
	headers := []string{
		"[Event \"Unknown Event\"]",
		"[Site \"Unknown Site\"]",
		"[Date \"????.??.??\"]",
		"[Round \"?\"]",
		"[White \"Unknown\"]",
		"[Black \"Unknown\"]",
		"[Result \"*\"]",
		"",
	}

	// Write headers to buffer
	for _, header := range headers {
		_, err := buffer.WriteString(header + "\n")
		if err != nil {
			return "", err
		}
	}

	// Write the moves in PGN format.
	for i, move := range moves {
		// PGN format requires move numbers before each White's move.
		if i%2 == 0 {
			_, err := buffer.WriteString(fmt.Sprintf("%d. ", (i/2)+1))
			if err != nil {
				return "", err
			}
		}

		// Write the move itself.
		_, err := buffer.WriteString(fmt.Sprintf("%s ", move))
		if err != nil {
			return "", err
		}

		// Add a newline every full move (after Black's move) for readability.
		if i%2 == 1 {
			_, err := buffer.WriteString("\n")
			if err != nil {
				return "", err
			}
		}
	}

	// Add the result at the end. Placeholder as the actual result might need to be determined.
	_, err := buffer.WriteString("\n*")
	if err != nil {
		return "", err
	}

	return buffer.String(), nil
}
