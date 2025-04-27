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

// ExtractTextFromImage uses OpenAI's vision model to extract text from an image.
func ExtractTextFromImage(apiKey string, imageBytes []byte) (string, error) {
	// Create an OpenAI client with the provided API key
	client := openai.NewClient(apiKey)

	// Convert image bytes to base64
	base64Image := base64.StdEncoding.EncodeToString(imageBytes)

	// Create a context with a timeout
	ctx, cancel := context.WithTimeout(context.Background(), 30*time.Second)
	defer cancel()

	// Create the chat completion request with the image
	resp, err := client.CreateChatCompletion(
		ctx,
		openai.ChatCompletionRequest{
			Model: openai.GPT4VisionPreview,
			Messages: []openai.ChatCompletionMessage{
				{
					Role: openai.ChatMessageRoleUser,
					MultiContent: []openai.ChatMessagePart{
						{
							Type: openai.ChatMessagePartTypeText,
							Text: "Extract and return only the text visible in this image. If it's a chess notation or move, just return the exact notation without any explanation.",
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
			MaxTokens: 300,
		},
	)

	if err != nil {
		return "", err
	}

	// Extract the text from the response
	if len(resp.Choices) > 0 {
		return resp.Choices[0].Message.Content, nil
	}

	return "", nil
}
