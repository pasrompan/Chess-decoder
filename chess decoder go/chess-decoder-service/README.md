# Chess Decoder Service

## Overview
The Chess Decoder Service is a web application that allows users to upload images of chess positions and receive the corresponding PGN (Portable Game Notation) file. The service utilizes image processing and text extraction techniques to convert chess moves from images into a standardized format.

## Features
- Upload an image of a chess position.
- Extract chess moves from the image using OCR (Optical Character Recognition).
- Convert extracted moves into PGN format.
- Return the generated PGN file as a response.

## Project Structure
```
chess-decoder-service
├── cmd
│   └── server
│       └── main.go          # Entry point of the application
├── internal
│   ├── api
│   │   ├── handlers.go      # HTTP handlers for API endpoints
│   │   ├── middleware.go     # Middleware functions for request processing
│   │   └── routes.go        # Route definitions for the application
│   ├── config
│   │   └── config.go        # Configuration management
│   ├── models
│   │   └── models.go        # Data structures for the application
│   └── service
│       └── chess_decoder.go  # Core logic for processing images and generating PGN
├── pkg
│   ├── images
│   │   └── processor.go      # Utility functions for image processing
│   └── pgn
│       └── converter.go      # Functions for converting moves to PGN format
├── go.mod                    # Go module definition
├── go.sum                    # Checksums for module dependencies
├── .env.example              # Template for environment variables
└── README.md                 # Project documentation
```

## Setup Instructions
1. **Clone the repository:**
   ```
   git clone <repository-url>
   cd chess-decoder-service
   ```

2. **Install dependencies:**
   ```
   go mod tidy
   ```

3. **Create a `.env` file:**
   Copy the `.env.example` file to `.env` and fill in the required environment variables.

4. **Run the application:**
   ```
   go run cmd/server/main.go
   ```

5. **Access the API:**
   The service will be available at `http://localhost:8080`. You can use tools like Postman or curl to test the image upload endpoint.

## Usage
To upload an image and receive a PGN file:
- Send a POST request to `/api/upload` with the image file in the request body.
- The response will contain the generated PGN file.

## Contributing
Contributions are welcome! Please open an issue or submit a pull request for any enhancements or bug fixes.

## License
This project is licensed under the MIT License. See the LICENSE file for more details.