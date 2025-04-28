package api

import (
	"bufio"
	"fmt"
	"net"
	"net/http"

	"github.com/gin-gonic/gin"
	"github.com/gorilla/mux"
)

// RegisterRoutes sets up the routes for the application.
func RegisterRoutes(r *mux.Router) {
	r.HandleFunc("/upload", func(w http.ResponseWriter, r *http.Request) {
		c := &gin.Context{
			Request: r,
			Writer:  &responseWriterAdapter{w},
		}
		UploadImageHandler(c)
	}).Methods("POST")
}

// responseWriterAdapter adapts http.ResponseWriter to gin.ResponseWriter
type responseWriterAdapter struct {
	http.ResponseWriter
}

func (r *responseWriterAdapter) Status() int {
	return http.StatusOK // Default value, can be enhanced
}

func (r *responseWriterAdapter) Size() int {
	return 0 // Default value, can be enhanced
}

func (r *responseWriterAdapter) Written() bool {
	return true // Default value, can be enhanced
}

func (r *responseWriterAdapter) WriteHeaderNow() {}

func (r *responseWriterAdapter) Pusher() http.Pusher {
	return nil
}

func (r *responseWriterAdapter) CloseNotify() <-chan bool {
	if cn, ok := r.ResponseWriter.(http.CloseNotifier); ok {
		return cn.CloseNotify()
	}
	return nil
}

func (r *responseWriterAdapter) Flush() {
	if flusher, ok := r.ResponseWriter.(http.Flusher); ok {
		flusher.Flush()
	}
}

func (r *responseWriterAdapter) Hijack() (net.Conn, *bufio.ReadWriter, error) {
	if hijacker, ok := r.ResponseWriter.(http.Hijacker); ok {
		return hijacker.Hijack()
	}
	return nil, nil, fmt.Errorf("responseWriterAdapter does not implement http.Hijacker")
}

func (r *responseWriterAdapter) WriteString(s string) (int, error) {
	return r.ResponseWriter.Write([]byte(s))
}
