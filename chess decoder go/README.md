# Chess Moves Translator

This project is a Go program designed to read a file containing chess moves written in Greek characters, convert them into English characters, and then output the game in Portable Game Notation (PGN) format.

## Getting Started

To use this program, you will need to have Go installed on your machine. You can download Go from [the official website](https://golang.org/dl/).

### Installation

Clone this repository to your local machine to get started with the Chess Moves Translator:

```
git clone https://yourrepositoryurl.git
```

Navigate into the project directory:

```
cd path_to_cloned_repository
```

### Usage

The program expects a file path to the input file as an argument. The input file should contain chess moves written in Greek notation. An example input file is provided in the `testdata` directory.

To run the program, use the following command from the root of the project directory:

```
go run main.go testdata/input.txt
```

This will read the input file, translate the moves into English, generate the PGN content, and save it to an output file named `output.pgn` in the project directory.

## Project Structure

The project consists of the following files:

- `main.go`: The entry point of the program. It handles reading the input file, invoking the translation and PGN generation functions, and writing the output.
- `chessmoves.go`: Contains the logic for converting chess moves from Greek to English notation.
- `pgnwriter.go`: Responsible for generating the PGN format content and writing it to the output file.
- `utils.go`: Includes utility functions for reading from and writing to files, and translating moves.
- `testdata/input.txt`: An example input file containing chess moves in Greek notation.
- `README.md`: This file, providing an overview and instructions for using the program.

## Contributing

Contributions to the Chess Moves Translator are welcome. Please feel free to submit pull requests or open issues to suggest improvements or report bugs.

## License

This project is licensed under the MIT License - see the LICENSE file for details.
