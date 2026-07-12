package main

import (
	"fmt"
	"os"
	"time"
	"github.com/sdwck/ToggleMesh/sdks/go/togglemesh"
)

func main() {
	apiKey := os.Getenv("TOGGLEMESH_API_KEY")
	if apiKey == "" {
		fmt.Println("TOGGLEMESH_API_KEY environment variable is required.")
		os.Exit(1)
	}

	options := &togglemesh.ToggleMeshOptions{
		BaseURL: "http://localhost:5264",
		APIKey:  apiKey,
	}

	client, err := togglemesh.NewClient(options)
	if err != nil {
		panic(err)
	}

	fmt.Println("Go SDK started. Simulating traffic...")
	for {
		uid := "user_go_1"
		context := map[string]any{
			"Browser": "Safari",
		}

		variation := client.GetStringVariation("mab-string-test", "default-variant", 
			togglemesh.WithIdentity(uid), 
			togglemesh.WithContext(context),
		)
		fmt.Printf("[Go SDK] Evaluated mab-string-test for %s: %s\n", uid, variation)

		if time.Now().UnixNano()%3 == 0 {
			client.Track("purchase", 
				togglemesh.WithIdentity(uid), 
				togglemesh.WithContext(map[string]any{"sdk": "go"}),
				togglemesh.WithEventValue(15.0),
			)
			fmt.Printf("[Go SDK] Tracked 'purchase' for %s\n", uid)
		}

		time.Sleep(1500 * time.Millisecond)
	}
}
