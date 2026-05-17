// Compat runner: parse os.Args[1] via godotenv and print parsed JSON.
package main

import (
	"encoding/json"
	"fmt"
	"os"

	"github.com/joho/godotenv"
)

func main() {
	if len(os.Args) != 2 {
		fmt.Fprintln(os.Stderr, "usage: run <fixture.env>")
		os.Exit(2)
	}
	env, err := godotenv.Read(os.Args[1])
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	b, err := json.Marshal(env)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	fmt.Println(string(b))
}
