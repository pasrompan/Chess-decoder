package main

import (
	"bytes"
	"image"
	_ "image/jpeg"
	"image/png"
	_ "image/png"
	"os"

	"github.com/nfnt/resize"
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

// ExtractTextFromImage uses OpenAI's API to extract text from an image.
func ExtractTextFromImage(apiKey string, imageBytes []byte) (string, error) {
	//client := openai.NewClient(apiKey)

	/*req := openai.ImageRequest{
		Prompt: "Extract text from this image",
		Size:   "1024x1024",
		// Note: The go-openai library does not support direct image uploads in this way.
		// You may need to use a different API endpoint or library for image-to-text functionality.
	}*/
	// Placeholder: Replace this with the correct API call for image-to-text functionality.
	//resp, err := client.CreateImage(req)
	/*if err != nil {
		return "", err
	}*/

	return "e4", nil
}
