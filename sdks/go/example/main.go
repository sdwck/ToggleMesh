package main

import (
	"fmt"
	"time"
	"github.com/sdwck/ToggleMesh/sdks/go/togglemesh"
)

func main() {
	options := &togglemesh.ToggleMeshOptions{
		BaseURL:   "http://localhost:5264",
		APIKey: "tm_server_w9pYCQFCJj3DdXuzsQfWvZNSIjE1x0zrMSv5PHvrW8",
	}

	client, err := togglemesh.NewClient(options)
	if err != nil {
		panic(err)
	}

	fmt.Println("Starting flag evaluation loop (press Ctrl+C to exit)...")
	for {
		email := "nirawolker@gmail.com"
		uid := "123456"
		context := map[string]any{
			"Email":  email,
			"UserId": uid,
		}

		enabled := client.IsEnabled(Gmail20percent, false, uid, context)
		
		if enabled {
			client.Track("go_gmail_checked", 1.0, uid, context)
			fmt.Printf("[Gmail 20%%] %s -> ENABLED!\n", email)
		} else {
			fmt.Printf("[Gmail 20%%] %s -> DISABLED.\n", email)
		}

		time.Sleep(3 * time.Second)
	}
}
