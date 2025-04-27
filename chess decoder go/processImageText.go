package main

import (
	"bytes"
	"context"
	"encoding/base64"
	"image"
	_ "image/jpeg"
	"image/png"
	_ "image/png"
	"os"
	"time"

	"github.com/nfnt/resize"
	"github.com/sashabaranov/go-openai"
)

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

	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
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
