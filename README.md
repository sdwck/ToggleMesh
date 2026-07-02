<div align="center">
  <h1>🚀 ToggleMesh</h1>
  <p><b>Enterprise-Grade, Zero-Allocation Feature Flag & Configuration Engine</b></p>
  <p>A self-hosted, blazing-fast alternative to LaunchDarkly, natively built for the .NET ecosystem.</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-purple" />
  <img src="https://img.shields.io/badge/Latency-37.7ns-success" />
  <img src="https://img.shields.io/badge/Allocations-0B-blue" />
  <img src="https://img.shields.io/badge/License-MIT-green" />
</p>

---

## 📚 Navigation

- [⚡ The 37-Nanosecond Engine](#-the-37-nanosecond-engine)
- [🏗️ Architecture](#️-architecture-control-plane-vs-data-plane)
- [✨ Key Features](#-key-features)
  - [🛡️ Unbreakable Resilience](#️-unbreakable-resilience)
  - [🎯 Advanced Targeting Engine](#-advanced-targeting-engine)
  - [📊 High-Throughput Metrics Ingestion](#-high-throughput-metrics-ingestion)
  - [🔐 Enterprise Security & Multi-Tenancy](#-enterprise-security--multi-tenancy)
- [👨‍💻 Type-Safe CLI Generator](#-type-safe-cli-generator)
- [🚀 Quick Start](#-quick-start-sdk-integration)
- [💻 Tech Stack](#-tech-stack)

---

## ⚡ The 37-Nanosecond Engine
ToggleMesh is built for extreme high-load environments where feature flags are evaluated in hot paths. The C# SDK evaluates targeting rules locally in memory, requiring **zero HTTP network calls** during execution.

By leveraging compiled Expression Trees, pre-computed rule groups, and `readonly ref struct` contexts, the ToggleMesh SDK achieves **Absolute Zero Heap Allocations**.

### 📊 BenchmarkDotNet Results
| Method              | Mean     | Error   | StdDev  | Gen0   | Allocated |
|-------------------- |---------:|--------:|--------:|-------:|----------:|
| IsEnabled_WithRules | **37.7 ns** | 1.54 ns | 1.44 ns | 0.0000 |   **-**   |

*(1 millisecond = 1,000,000 nanoseconds. Your servers will not even notice ToggleMesh is running, and the Garbage Collector remains completely unbothered).*

**Benchmark environment:** Intel Core i7-14700K, .NET 10, Windows 11 x64, Release build, BenchmarkDotNet.

---

## 🏗️ Architecture: Control Plane vs. Data Plane

ToggleMesh isolates management from execution to guarantee 100% uptime for your application.

1. **Control Plane (Management API & UI):** Built with .NET 10, FastEndpoints, and PostgreSQL. Features a sleek Vercel-style React/Tailwind Admin Dashboard. Managers can toggle flags, clone environments, and define complex targeting rules.
2. **Data Plane (SDK & SignalR):** When a flag changes, the API broadcasts the update via **SignalR** (backed by a Redis backplane). Connected SDKs instantly update their local `ConcurrentDictionary` cache.

---

## ✨ Key Features

### 🛡️ Unbreakable Resilience
The SDK is designed to survive network partitions and API outages:
* **Circuit Breakers & Exponential Backoff:** Automatic retries and circuit breaking (powered by Polly) prevent self-DDoS.
* **Offline Fallback:** The SDK saves flag states to a local `.json` fallback file. If your application restarts while the ToggleMesh API is down, it boots seamlessly using the last known state.

### 🎯 Advanced Targeting Engine
Evaluate users against nested **AND/OR** rule groups using built-in operators:
* `Equals`, `Contains`, `StartsWith`, `EndsWith`, `Regex`, `InList`.
* `SemVerGreaterThan`, `SemVerLessThan` (Perfect for mobile app version targeting).
* **Incremental Rollouts (A/B Testing):** Deterministic percentage-based rollouts using FNV-1a hashing ensures a user consistently receives the same flag state.

### 📊 High-Throughput Metrics Ingestion
The SDK automatically batches feature evaluations (True/False exposure counts) and flushes them to the server every 10 seconds. The API ingests these batches via `System.Threading.Channels` and background workers process them into SQL via `ExecuteSqlAsync` with `unnest` arrays, easily handling **tens of thousands of RPS** without bottlenecking the database or risking deadlocks.

### 🔐 Enterprise Security & Multi-Tenancy
* **RBAC:** Built-in Identity with Owner, Admin, Editor, and Viewer roles scoped per project.
* **Hashed API Keys:** Environment SDK keys are hashed (SHA-256) before entering the database.
* **Audit Logs:** EF Core `SaveChangesInterceptors` automatically track every mutation (Who, What, When, and JSON diffs) for compliance.

---

## 👨‍💻 Type-Safe CLI Generator

Tired of typing magic strings like `_client.IsEnabled("new_checkout")`? 
ToggleMesh includes a built-in CLI tool that connects to your environment and generates strongly-typed constants for your codebase. 

Supported outputs: **C#, TypeScript, JavaScript, Python, Go**.

```bash
$ togglemesh sync --lang typescript --out ./src/flags.ts
Fetching flags from Control Plane...
✔  Success! Generated 12 flags to flags.ts
```

```typescript
// Use generated constants in your code
import { Flags } from './flags';

if (client.isEnabled(Flags.NewCheckoutFlow)) {
    // ...
}
```

---

## 🚀 Quick Start (SDK Integration)

### 1. Register the Client in your DI Container
```csharp
// Program.cs
builder.Services.AddToggleMeshClient(options => 
{
    options.BaseUrl = "https://api.togglemesh.dev";
    options.ApiKey = "tm_your_environment_api_key_here";
});

// Optional: Automatically inject user context from HttpContext/JWT
builder.Services.AddToggleMeshHttpContext();
```

### 2. Evaluate in your Business Logic
```csharp
public class CheckoutService(IToggleMeshClient _toggleMesh)
{
    public void ProcessOrder()
    {
        // Evaluated in ~37ns. No HTTP request made.
        if (_toggleMesh.IsEnabled("next-gen-payment-gateway")) 
        {
            ExecuteNewGateway();
        }
    }
}
```

### 3. Evaluate with Custom Context
```csharp
var userContext = new { Email = "user@company.com", Plan = "Premium" };

if (_toggleMesh.IsEnabled("beta-feature", contextObject: userContext))
{
    // ...
}
```

---

## 💻 Tech Stack
* **Backend:** C#, .NET 10, FastEndpoints, EF Core, SignalR
* **Infrastructure:** PostgreSQL, Redis, Docker Compose
* **Frontend:** React, Vite, TypeScript, Tailwind CSS, Shadcn UI
* **Quality Assurance:** `Testcontainers` for Integration Testing, `FakeTimeProvider` for deterministic async testing, `k6` for load testing.
}
```

---

## 💻 Tech Stack
* **Backend:** C#, .NET 10, FastEndpoints, EF Core, SignalR
* **Infrastructure:** PostgreSQL, Redis, Docker Compose
* **Frontend:** React, Vite, TypeScript, Tailwind CSS, Shadcn UI
* **Quality Assurance:** `Testcontainers` for Integration Testing, `FakeTimeProvider` for deterministic async testing, `k6` for load testing.
