# Contributing to ToggleMesh

First off, thank you for considering contributing to ToggleMesh! It's people like you that make ToggleMesh a great tool for the .NET community.

## 🚀 Development Setup

To work on the ToggleMesh Control Plane and SDKs, you need:
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker & Docker Compose](https://www.docker.com/)
- [Node.js](https://nodejs.org/) (for the React Admin UI)

### Running Locally
1. Clone the repository.
2. Spin up the local infrastructure (PostgreSQL, Redis):
   ```bash
   docker-compose -f docker-compose.yml up -d
   ```
3. Run the API:
   ```bash
   cd src/ToggleMesh.API
   dotnet run
   ```
4. Run the Admin UI:
   ```bash
   cd src/ToggleMesh.AdminUI
   npm install
   npm run dev
   ```

## 🧪 Testing

We take reliability seriously. ToggleMesh uses `Testcontainers` for integration testing. 
Before submitting a Pull Request, ensure all tests pass:
```bash
dotnet test
```

If you are modifying the Evaluation Engine hot path (`ToggleMesh.SDK/Rules`), please run the benchmarks to ensure zero-allocation performance is maintained:
```bash
cd benchmarks/ToggleMesh.Benchmarks
dotnet run -c Release
```

## 💅 Code Style & Philosophy

We strive for clean, readable, and highly performant code.
- **C# Conventions:** Please adhere to standard C# naming and formatting conventions. When in doubt, run `dotnet format` before committing.
- **Zero-Allocation:** If you are modifying the Evaluation Engine hot path (e.g., inside `ToggleMesh.SDK/Rules`), keep the "Zero-Allocation" philosophy in mind. Avoid LINQ, Boxing, or unnecessary heap allocations.

## 📝 Pull Request Process

1. **Discuss First:** For anything beyond a simple bug fix or typo, please **open an issue first** to discuss your proposed change before investing significant effort. We want to ensure your work aligns with the project's architectural vision.
2. Fork the repo and create your branch from `main`.
3. If you've added code that should be tested, add integration tests.
4. Update the `README.md` with details of changes to the interface, if applicable.
5. Open a Pull Request. Provide a clear description of the problem you solved or the feature you added.

## 💬 Commit Convention
We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification. Please format your commit messages accordingly (e.g., `feat: added semantic versioning operator`, `fix: resolved redis connection timeout`).
