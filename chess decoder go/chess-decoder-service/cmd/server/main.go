package main

import (
	"log"
	"net/http"

	"chess-decoder-service/internal/api"

	"github.com/gorilla/mux"
	"github.com/joho/godotenv"
)

func main() {
	// Load environment variables
	err := godotenv.Load()
	if err != nil {
		log.Println("Warning: Error loading .env file:", err)
	}

	// Initialize the router
	router := mux.NewRouter()

	// Set up routes
	api.RegisterRoutes(router)

	// Start the server
	port := ":8080"
	log.Printf("Starting server on port %s\n", port)
	if err := http.ListenAndServe(port, router); err != nil {
		log.Fatalf("Failed to start server: %s", err)
	}
}
