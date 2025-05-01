## GitHub Copilot
Chess Decoder API
A C# REST API service that converts chess game images into PGN (Portable Game Notation) format.

## Overview
Chess Decoder API processes images of chess games (scoresheets, diagrams, or photos of games in progress) and extracts the chess moves using computer vision. The extracted moves are then formatted into standard PGN notation that can be imported into chess software.

## Prerequisites
.NET 9.0 SDK or later
An OpenAI API key (for the image recognition feature)

## Environment Configuration

The application uses a `.env` file for local development configuration:

1. Create a `.env` file in the root directory of the project
2. Add your OpenAI API key:

## Configuration
Before running the application, you need to set up your environment variables:

## Setting the OpenAI API Key
On macOS/Linux:
On Windows (Command Prompt):
On Windows (PowerShell):

## Running the Application
From the Command Line
Navigate to the project directory:
Build the application:
Run the application:
The API will be available at http://localhost:5100 by default.

Using Visual Studio Code
Open the project folder in VS Code
Make sure you have the C# extension installed
Press F5 to start debugging, or Ctrl+F5 to run without debugging
Using Visual Studio
Open the ChessDecoderApi.csproj file in Visual Studio
Press F5 to start debugging, or Ctrl+F5 to run without debugging

## API Endpoints
POST /ChessDecoder/upload
Uploads an image of a chess game and returns a PGN file.

### Request
Method: POST
Content-Type: multipart/form-data
Body: Form field named "image" containing the image file

### Response
Content-Type: application/octet-stream
Body: PGN file content
Content-Disposition: attachment; filename=original_filename.pgn

### Status Codes
200 OK: Successfully processed image and generated PGN
400 Bad Request: No image provided or invalid file format
401 Unauthorized: Invalid or missing OpenAI API key
500 Internal Server Error: Server error during processing

## Testing the API
You can test the API using tools like Postman, curl, or the included HTTP request file:

### Using curl
### Using the .http File in VS Code
If you have the REST Client extension installed in VS Code, you can use the included ChessDecoderApi.http file:

Open the ChessDecoderApi.http file
Add a POST request for the upload endpoint
Click "Send Request" above the request
Example request to add to the .http file:

## Development
### Project Structure
Controllers/: Contains the API endpoint controllers
Services/: Contains the business logic for processing images
Models/: Contains data models and response types

### Adding New Features
Implement new service methods in the appropriate service class
Add new endpoints in the controller classes
Update models as needed
Add tests for new functionality

## License
MIT License