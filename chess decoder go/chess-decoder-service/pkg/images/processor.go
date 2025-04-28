package images

import (
	"bytes"
	"image"
	"image/jpeg"
	"os"
)

// LoadImage loads an image from the specified file path.
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

// ResizeImage resizes the given image to the specified width and height.
func ResizeImage(img image.Image, width, height int) image.Image {
	// Implement resizing logic here (e.g., using a third-party library)
	return img // Placeholder: return the original image for now
}

// ImageToBytes converts the image to a byte slice.
func ImageToBytes(img image.Image) ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := jpeg.Encode(buf, img, nil); err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
}
