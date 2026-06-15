# 🚀 ToggleMesh CLI Tool

`ToggleMesh.CLI` is a powerful, lightweight .NET Global Tool designed to synchronize feature flags and configuration schemas directly from your **ToggleMesh** Control Plane into your local codebase as strongly-typed constants.

Eliminate manual typos and out-of-sync configuration strings by automating code generation.

---

## 📦 Installation

To install `ToggleMesh.CLI` globally on your machine, run the following command in your terminal:

```bash
dotnet tool install --global ToggleMesh.CLI
```

---

## ⚙️ Quick Start

### 1. Configure your environment
Run the interactive configuration wizard to link the CLI to your ToggleMesh environment:

```bash
togglemesh config
```
*You will be prompted to enter your **Endpoint URL** and your secure **API Key** (`tm_server_...` or `tm_client_...`).*

### 2. Synchronize and Generate Constants
Generate type-safe constants for your primary development language.

```bash
togglemesh sync --lang typescript --out ./src/flags.ts
```

---

## 🛠️ Supported Output Languages

ToggleMesh CLI can generate native strongly-typed constants for:
*   **C#** (Creates type-safe class properties)
*   **TypeScript / JavaScript** (Creates strict type definitions)
*   **Go** (Generates native typed constants)
*   **Python** (Generates typed string constants)

---

## 🔗 Resources
*   **Main Repository:** [GitHub - sdwck/ToggleMesh](https://github.com/sdwck/ToggleMesh)