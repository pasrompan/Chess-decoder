package api

import (
	"errors"
	"fmt"
	"net/http"
	"os"
	"path/filepath"
	"strings"

	"chess-decoder-service/internal/service"

	"github.com/gin-gonic/gin"
)

// UploadImageHandler handles the POST request to upload an image and return the generated PGN file.
func UploadImageHandler(c *gin.Context) {
	// Parse the multipart form
	if err := c.Request.ParseMultipartForm(10 << 20); err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "Invalid form data"})
		return
	}

	// Get the file from the form
	file, err := c.FormFile("image")
	if err != nil {
		c.JSON(http.StatusBadRequest, gin.H{"error": "No image file provided"})
		return
	}

	// Save the uploaded file to a temporary location
	tempFilePath := fmt.Sprintf("temp/%s", file.Filename)
	if err := c.SaveUploadedFile(file, tempFilePath); err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to save uploaded file"})
		return
	}
	defer os.Remove(tempFilePath) // Clean up the temporary file

	// Process the image and generate PGN
	pgnContent, err := ProcessImage(tempFilePath)
	if err != nil {
		c.JSON(http.StatusInternalServerError, gin.H{"error": "Failed to process image"})
		return
	}

	// Set the response headers for file download
	c.Header("Content-Disposition", fmt.Sprintf("attachment; filename=%s.pgn", strings.TrimSuffix(file.Filename, filepath.Ext(file.Filename))))
	c.Header("Content-Type", "application/octet-stream")
	c.String(http.StatusOK, pgnContent)
}

// ProcessImage processes the given image file and returns the generated PGN content
func ProcessImage(imagePath string) (string, error) {
	// Check if file exists
	_, err := os.Stat(imagePath)
	if err != nil {
		return "", errors.New("image file not found")
	}

	// Load the image
	img, err := service.LoadImage(imagePath)
	if err != nil {
		return "", fmt.Errorf("failed to load image: %w", err)
	}

	// Resize the image if necessary
	img = service.ResizeImage(img, 1024, 1024)

	// Convert the image to bytes
	imageBytes, err := service.ImageToBytes(img)
	if err != nil {
		return "", fmt.Errorf("failed to convert image to bytes: %w", err)
	}

	// Extract text from the image using OpenAI
	apiKey := os.Getenv("OPENAI_API_KEY")
	if apiKey == "" {
		return "", errors.New("OPENAI_API_KEY environment variable not set")
	}

	language := "English" // Default to English language
	text, err := service.ExtractTextFromImage(apiKey, imageBytes, language)
	if err != nil {
		return "", fmt.Errorf("failed to extract text from image: %w", err)
	}

	// Convert the extracted text to PGN format
	var englishMoves []string
	if language == "Greek" {
		englishMoves, err = service.ConvertGreekMovesToEnglish(strings.Split(text, "\n"))
		if err != nil {
			return "", fmt.Errorf("failed to convert Greek moves to English: %w", err)
		}
	} else {
		englishMoves = strings.Split(text, "\n")
	}

	// Generate the PGN content
	pgnContent, err := service.GeneratePGNContent(englishMoves)
	if err != nil {
		return "", fmt.Errorf("failed to generate PGN content: %w", err)
	}

	return pgnContent, nil
}
