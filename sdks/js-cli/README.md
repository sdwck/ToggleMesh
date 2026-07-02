# 🚀 ToggleMesh JS/TS CLI Tool

[![GitHub Repo](https://img.shields.io/badge/GitHub-ToggleMesh-blue?logo=github)](https://github.com/sdwck/ToggleMesh)

The official **ToggleMesh CLI** specifically packaged for Node.js environments.
This tool automates the process of synchronizing feature flags from your ToggleMesh Control Plane and generating strongly-typed definitions (like TypeScript Enums and Types) directly into your codebase.

## 📦 Installation

Install globally via NPM:

```bash
npm install -g togglemesh
```

Or run directly using `npx`:

```bash
npx togglemesh sync --lang typescript --out ./src/flags.ts
```

## ⚙️ Usage

### 1. Configure the CLI
Set up your connection to the ToggleMesh control plane securely. This will cache your API key locally.
```bash
togglemesh config
```

### 2. Sync and Generate Flags
Synchronize the flags and generate native TypeScript/JavaScript constants:
```bash
togglemesh sync --lang typescript --out ./src/shared/flags.ts
```

## ✨ Features
*   **Strongly-Typed Flags:** Eliminates hardcoded strings and typos by generating native `enum` or `const` objects.
*   **Zero Dependencies Output:** The generated file requires no external dependencies.
*   **Seamless Integration:** Perfectly pairs with `togglemesh-js` and `togglemesh-node` SDKs.

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.
