package models

type ChessMove struct {
	Move   string `json:"move"`
	Player string `json:"player"`
}

type PGNFile struct {
	Event string      `json:"event"`
	Site  string      `json:"site"`
	Date  string      `json:"date"`
	Round string      `json:"round"`
	White string      `json:"white"`
	Black string      `json:"black"`
	Moves []ChessMove `json:"moves"`
}