package main

import (
	"bytes"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"
)

type SdkFlagsResponse struct {
	Flags []struct {
		Key string `json:"key"`
	} `json:"flags"`
}

type ProjectFlagsResponse struct {
	Data []struct {
		Key string `json:"key"`
	} `json:"items"`
}

type ConfigData map[string]string

func main() {
	if len(os.Args) < 2 {
		printUsage()
		os.Exit(1)
	}

	switch os.Args[1] {
	case "config":
		handleConfig(os.Args[2:])
	case "sync":
		handleSync(os.Args[2:])
	default:
		fmt.Printf("Unknown command: %s\n\n", os.Args[1])
		printUsage()
		os.Exit(1)
	}
}

func printUsage() {
	fmt.Println("ToggleMesh Go CLI")
	fmt.Println("Usage:")
	fmt.Println("  togglemesh <command> [arguments]")
	fmt.Println("\nCommands:")
	fmt.Println("  config    Configure global or local settings")
	fmt.Println("  sync      Synchronize feature flags from server")
}

func handleConfig(args []string) {
	configCmd := flag.NewFlagSet("config", flag.ExitOnError)
	key := configCmd.String("key", "", "API Key or PAT (saved globally)")
	address := configCmd.String("address", "", "Server address (saved globally)")
	project := configCmd.String("project", "", "Project ID for PAT (saved locally)")
	out := configCmd.String("out", "", "Output path (saved locally)")

	configCmd.Parse(args)

	if *key == "" && *address == "" && *out == "" && *project == "" {
		fmt.Println("No configuration options provided. Use -key, -address, -project, or -out.")
		os.Exit(1)
	}

	home, err := os.UserHomeDir()
	if err == nil && (*key != "" || *address != "") {
		globalDir := filepath.Join(home, ".togglemesh")
		os.MkdirAll(globalDir, 0755)
		
		globalPath := filepath.Join(globalDir, "config.json")
		data := loadJson(globalPath)
		
		if *key != "" {
			data["ApiKey"] = *key
		}
		if *address != "" {
			data["BaseUrl"] = *address
		}
		saveJson(globalPath, data)
		fmt.Printf("Saved global config to %s\n", globalPath)
	}

	if *out != "" || *project != "" {
		cwd, err := os.Getwd()
		if err == nil {
			localDir := filepath.Join(cwd, ".togglemesh")
			os.MkdirAll(localDir, 0755)
			
			localPath := filepath.Join(localDir, "config.json")
			data := loadJson(localPath)
			if *out != "" {
				data["OutPath"] = *out
			}
			if *project != "" {
				data["ProjectId"] = *project
			}
			saveJson(localPath, data)
			fmt.Printf("Saved local config to %s\n", localPath)
		}
	}
}

func handleSync(args []string) {
	syncCmd := flag.NewFlagSet("sync", flag.ExitOnError)
	key := syncCmd.String("key", "", "API Key or PAT (overrides config)")
	address := syncCmd.String("address", "", "Server address (overrides config)")
	project := syncCmd.String("project", "", "Project ID for PAT (overrides config)")
	out := syncCmd.String("out", "", "Output path (overrides config)")
	pkg := syncCmd.String("pkg", "togglemesh", "Go package name for the generated file")

	syncCmd.Parse(args)

	cfgKey, cfgAddress, cfgProject, cfgOut := resolveConfig()

	finalKey := cfgKey
	if *key != "" {
		finalKey = *key
	}
	finalAddress := cfgAddress
	if *address != "" {
		finalAddress = *address
	}
	if finalAddress == "" {
		finalAddress = "http://localhost:5264"
	}
	finalProject := cfgProject
	if *project != "" {
		finalProject = *project
	}
	finalOut := cfgOut
	if *out != "" {
		finalOut = *out
	}
	if finalOut == "" {
		finalOut = "flags.go"
	}

	if finalKey == "" {
		fmt.Println("Error: API Key is required. Run 'togglemesh config -key <YOUR_KEY>' or pass -key.")
		os.Exit(1)
	}

	isPat := strings.HasPrefix(strings.ToLower(finalKey), "tmp_")
	if isPat && finalProject == "" {
		fmt.Println("Error: Personal Access Token (PAT) detected, but -project is missing. Please provide your Project ID.")
		os.Exit(1)
	}

	var url string
	if isPat {
		url = fmt.Sprintf("%s/api/v1/projects/%s/flags", strings.TrimRight(finalAddress, "/"), finalProject)
	} else {
		url = strings.TrimRight(finalAddress, "/") + "/api/v1/sdk/flags"
	}
	
	req, err := http.NewRequest("GET", url, nil)
	if err != nil {
		fmt.Printf("Failed to create request: %v\n", err)
		os.Exit(1)
	}
	
	if isPat {
		req.Header.Set("x-pat-token", finalKey)
		req.Header.Set("x-environment-id", finalProject)
	} else {
		req.Header.Set("x-api-key", finalKey)
	}
	req.Header.Set("x-sdk-version", "go-cli-0.2.2")

	client := &http.Client{Timeout: 10 * time.Second}
	resp, err := client.Do(req)
	if err != nil {
		fmt.Printf("Failed to connect to ToggleMesh server: %v\n", err)
		os.Exit(1)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		body, _ := io.ReadAll(resp.Body)
		fmt.Printf("Server returned error %d: %s\n", resp.StatusCode, string(body))
		os.Exit(1)
	}

	var keys []string

	if isPat {
		var data ProjectFlagsResponse
		if err := json.NewDecoder(resp.Body).Decode(&data); err != nil {
			fmt.Printf("Failed to parse server response: %v\n", err)
			os.Exit(1)
		}
		for _, f := range data.Data {
			keys = append(keys, f.Key)
		}
	} else {
		var data SdkFlagsResponse
		if err := json.NewDecoder(resp.Body).Decode(&data); err != nil {
			fmt.Printf("Failed to parse server response: %v\n", err)
			os.Exit(1)
		}
		for _, f := range data.Flags {
			keys = append(keys, f.Key)
		}
	}

	var buf bytes.Buffer
	buf.WriteString("// <auto-generated>\n")
	buf.WriteString("// This file was generated by ToggleMesh Go CLI.\n")
	buf.WriteString("// Do not edit this file manually.\n")
	buf.WriteString("// </auto-generated>\n\n")
	buf.WriteString(fmt.Sprintf("package %s\n\n", *pkg))
	
	if len(keys) > 0 {
		buf.WriteString("const (\n")
		for _, k := range keys {
			pascalKey := toPascalCase(k)
			buf.WriteString(fmt.Sprintf("\t%s = \"%s\"\n", pascalKey, k))
		}
		buf.WriteString(")\n")
	} else {
		buf.WriteString("// No flags found on the server.\n")
	}

	if dir := filepath.Dir(finalOut); dir != "." {
		os.MkdirAll(dir, 0755)
	}

	if err := os.WriteFile(finalOut, buf.Bytes(), 0644); err != nil {
		fmt.Printf("Failed to write output file: %v\n", err)
		os.Exit(1)
	}

	fmt.Printf("Successfully generated %d flags to %s\n", len(keys), finalOut)
}

func resolveConfig() (apiKey, baseUrl, projectId, outPath string) {
	if home, err := os.UserHomeDir(); err == nil {
		globalPath := filepath.Join(home, ".togglemesh", "config.json")
		if data := loadJson(globalPath); data != nil {
			apiKey = data["ApiKey"]
			baseUrl = data["BaseUrl"]
		}
	}

	if cwd, err := os.Getwd(); err == nil {
		localPath := filepath.Join(cwd, ".togglemesh", "config.json")
		if data := loadJson(localPath); data != nil {
			if val, ok := data["ApiKey"]; ok && val != "" { apiKey = val }
			if val, ok := data["BaseUrl"]; ok && val != "" { baseUrl = val }
			if val, ok := data["ProjectId"]; ok && val != "" { projectId = val }
			if val, ok := data["OutPath"]; ok && val != "" { outPath = val }
		}
	}

	if val := os.Getenv("TOGGLEMESH__APIKEY"); val != "" { apiKey = val }
	if val := os.Getenv("TOGGLEMESH__BASEURL"); val != "" { baseUrl = val }
	if val := os.Getenv("TOGGLEMESH__PROJECTID"); val != "" { projectId = val }
	if val := os.Getenv("TOGGLEMESH__OUTPATH"); val != "" { outPath = val }

	return
}

func loadJson(path string) ConfigData {
	data := make(ConfigData)
	b, err := os.ReadFile(path)
	if err == nil {
		json.Unmarshal(b, &data)
	}
	return data
}

func saveJson(path string, data ConfigData) {
	b, _ := json.MarshalIndent(data, "", "  ")
	os.WriteFile(path, b, 0644)
}

func toPascalCase(s string) string {
	parts := strings.FieldsFunc(s, func(r rune) bool {
		return r == '-' || r == '_' || r == ' '
	})
	
	for i, part := range parts {
		if len(part) > 0 {
			parts[i] = strings.ToUpper(part[:1]) + part[1:]
		}
	}
	
	return strings.Join(parts, "")
}
