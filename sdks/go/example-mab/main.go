package main

import (
	"fmt"
	"math/rand"
	"os"
	"time"

	"github.com/google/uuid"
	"github.com/sdwck/ToggleMesh/sdks/go/togglemesh"
)

func main() {
	client, _ := togglemesh.NewClient(&togglemesh.ToggleMeshOptions{
		BaseURL: "http://localhost:5264",
		APIKey:  os.Getenv("TOGGLEMESH_API_KEY"),
	})
	fmt.Println("Go SDK MAB Simulation started (Safari -> Third)")

	r := rand.New(rand.NewSource(time.Now().UnixNano()))

	for {
		userId := uuid.New().String()
		variation := client.GetStringVariation("mab-string-test", "control", togglemesh.WithIdentity(userId), togglemesh.WithContext(map[string]any{"Browser": "Safari"}))
		fmt.Printf("[%s] Evaluated mab-string-test: %s\n", userId, variation)

		if variation == "Third" {
			if r.Float64() < 0.3 {
				fmt.Printf("[%s] Tracking conversion!\n", userId)
				client.Track("purchase",
					togglemesh.WithIdentity(userId),
					togglemesh.WithEventValue(10.0),
					togglemesh.WithContext(map[string]any{"sdk": "go"}),
				)
			}
		} else {
			if r.Float64() < 0.1 {
				fmt.Printf("[%s] Tracking conversion!\n", userId)
				client.Track("purchase",
					togglemesh.WithIdentity(userId),
					togglemesh.WithEventValue(10.0),
					togglemesh.WithContext(map[string]any{"sdk": "go"}),
				)
			}
		}
		time.Sleep(1 * time.Second)
	}
}
