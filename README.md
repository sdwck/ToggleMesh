<div align="center">
  <img src="src/ToggleMesh.AdminUI/src/assets/icon.png" alt="ToggleMesh Logo" width="120" />
  <h1>ToggleMesh</h1>
  <p><b>Enterprise Feature Flag & Contextual Experimentation Engine</b></p>
  <p>A high-performance, data-private, self-hosted alternative to LaunchDarkly and Statsig. Purpose-built for the .NET ecosystem.</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/Status-v1.0.0--rc-success" alt="Status" />
  <a href="https://github.com/sdwck/ToggleMesh/actions/workflows/publish_sdk.yml"><img src="https://github.com/sdwck/ToggleMesh/actions/workflows/publish_sdk.yml/badge.svg" alt="Build Status" /></a>
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet" alt=".NET 10" />
  <img src="https://img.shields.io/badge/Latency-%3C25ns-success" alt="<25ns Latency" />
  <img src="https://img.shields.io/badge/Allocations-0B-blue" alt="0 Bytes Allocated" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License" />
</p>

<!-- TODO: Add dashboard screenshot (docs/assets/dashboard-preview.png) -->
> *![ToggleMesh Admin Dashboard](docs/assets/dashboard-preview.png)*  
> *Manage environments, targeting rules, and A/B tests from a unified, modern interface.*

---

## 📖 What is ToggleMesh?

ToggleMesh is a self-hosted feature management and experimentation platform designed for teams that require strict data privacy, enterprise-grade RBAC, and ultra-low latency. 

Unlike SaaS providers that charge by MAU or require your application to make network calls for flag evaluation, ToggleMesh pushes configurations directly to your servers. Your SDKs evaluate rules **locally in memory**, ensuring zero network overhead and keeping 100% of your user context within your private infrastructure.

### How it compares

| Feature | ToggleMesh | LaunchDarkly | Statsig | Unleash |
|---|:---:|:---:|:---:|:---:|
| **Full Self-Hosting** | ✅ | ❌ *(Relay only)* | ❌ | ✅ |
| **Data Privacy (No PII leaves network)** | ✅ | ❌ | ❌ | ✅ |
| **Zero-Allocation Eval (.NET)**| ✅ | ❌ | ❌ | ❌ |
| **Built-in MAB A/B Tests** | ✅ | ❌ | ✅ | ❌ |
| **Real-Time Push** | ✅ *(SSE)* | ✅ | ✅ | ⚠️ *(Polling)* |
| **Pricing** | **Free (MIT)** | Per-seat | Per-MAU | Freemium |

<!-- TODO: Add architecture diagram (docs/assets/architecture-diagram.png) or embed a Mermaid diagram directly here -->
> *![ToggleMesh Architecture](docs/assets/architecture-diagram.png)*  
> *Control Plane mutates state -> Interceptor triggers Redis Pub/Sub -> API fans out SSE to connected SDKs.*

---

## 👨‍💻 Quick Start

> **Prerequisite:** A running ToggleMesh server instance. See [Self-Hosting & Deployment](#-self-hosting--deployment) to spin one up locally in under a minute.

### Step 0: Install the Tools
Install the C# SDK and the Global CLI tool:
```bash
dotnet add package ToggleMesh.SDK
dotnet tool install --global ToggleMesh.CLI
```

### Step 1: Zero-Config Context Injection
Register the SDK in your DI container. ToggleMesh can automatically hook into your ambient `HttpContext` to extract user identity, emails, and roles without manual context passing.

```csharp
// Program.cs
builder.Services.AddToggleMeshClient(options => 
{
    options.EndpointUrl = "https://api.togglemesh.dev"; // Your self-hosted Control Plane
    options.ApiKey = "tm_server_xxxxxxxx";
}).AddToggleMeshHttpContext(); 
```

### Step 2: Sync Constants & Evaluate
Sync your flags to generate type-safe constants, then evaluate them in your business logic.

```bash
$ togglemesh config
$ togglemesh sync
✔ Success! Auto-detected C# project. Generated ToggleMeshFlags.g.cs
```

```csharp
// CheckoutService.cs
public void ProcessOrder(IToggleMeshClient toggleMesh) 
{
    // Evaluates instantly from in-memory cache. Zero HTTP requests made.
    if (toggleMesh.IsEnabled(Flags.NewCheckoutFlow)) 
    {
        ExecuteNextGenGateway();
    }
}
```

---

## ⚡ The Sub-25ns Evaluation Engine (Performance Proof)

ToggleMesh is engineered for ultra-low-latency microservices where Garbage Collection (GC) pauses are unacceptable.

By leveraging compiled Expression Trees, pre-computed rule groups, C# Source Generators, and `readonly ref struct` contexts, the ToggleMesh SDK achieves **zero-allocation evaluation**.

### BenchmarkDotNet Results
*We benchmarked various scenarios: from a simple global toggle to a worst-case scenario evaluating 10 nested AND/OR targeting rules.*

| Method | Mean | StdDev | Max | P95 | Allocated |
|---|---:|---:|---:|---:|---:|
| **IsEnabled_NoRules_AOT** *(Baseline)* | **17.34 ns** | 0.026 ns | 17.39 ns | 17.38 ns | **-** |
| **IsEnabled_With1Rule_AOT** *(Typical)*| **22.49 ns** | 0.055 ns | 22.55 ns | 22.55 ns | **-** |
| **IsEnabled_Complex_AOT** *(MAB/Rollout)*| **81.44 ns** | 0.160 ns | 81.82 ns | 81.74 ns | **-** |
| **IsEnabled_With10Rules_AOT** *(Worst-case)* | **127.14 ns** | 0.664 ns | 128.12 ns | 127.93 ns | **-** |
| **Track_Event** *(Metrics Buffer)* | **41.58 ns** | 0.071 ns | 41.69 ns | 41.69 ns | **-** |

> **Hardware Specs:** Intel Core i7-14700K, 20 Physical Cores, Windows 11 x64, .NET 10.0 Release Build.

### Extreme High-Throughput (Load Testing)

ToggleMesh decouples heavy I/O operations from the HTTP request-response cycle using **bounded in-memory channels** (`System.Threading.Channels`) with `DropOldest` backpressure. 

Local load testing via [k6](https://k6.io/) on a single developer workstation demonstrates the massive throughput capabilities of the Data Plane API. 

| Endpoint | Type | Target VUs | Max RPS | p(99) Tail Latency | Error Rate |
|---|---|---:|---:|---:|---:|
| `POST /api/v1/sdk/metrics` | **Fire & Forget** (Channel) | 2,000 | **115,248/s** | 18.59 ms | 0.00% |
| `POST /api/v1/sdk/evaluate` | **Synchronous** (Compute) | 2,000 | **112,503/s** | 19.22 ms | 0.00% |
| `POST /api/v1/sdk/events` | **Buffered + Livetail** (SSE) | 2,000 | **68,301/s** | 34.58 ms | 0.00% |
| `GET /api/v1/sdk/flags` | **Synchronous** (I/O Cache) | 2,000 | **68,463/s** | 36.06 ms | 0.00% |

> **Test Environment:** Intel Core i7-14700K, `k6` running locally against Kestrel (Release mode, HTTP). All tests maintained a flawless 0.00% failure rate under sustained load. Data payload bandwidth maxed out at ~179 MB/s during sync.

---

## 💻 Ecosystem & Supported SDKs

ToggleMesh provides native SDKs and tooling for your entire microservice fleet.

| Language / Platform | SDK Type | Real-Time Sync | Targeting Evaluation | Maturity |
| :--- | :--- | :---: | :---: | :---: |
| **.NET (C#)** | Server | ✅ (SSE) | Local (Zero-Alloc) | Stable |
| **Node.js** | Server | ✅ (SSE) | Local | Beta |
| **Browser JS / React** | Client | ✅ (SSE) | Remote (Secure) | Beta |
| **Python** | Server | ✅ (SSE) | Local | Beta |
| **Go** | Server | ✅ (SSE) | Local | MVP |
| **Unreal Engine (C++)** | Game Client | 🔄 (Polling) | Remote | MVP |

---

## 🏢 Enterprise-Grade Features

- 📡 **Push, Not Pull (SSE + Redis):** Real-time cache invalidation using Server-Sent Events. No wasteful HTTP polling.
- 🧠 **Contextual Multi-Armed Bandits (MAB):** Built-in Bayesian inference engine (Monte Carlo simulations via Beta distributions). Autonomously shifts traffic toward winning variants based on conversion or revenue metrics.
- 📈 **High-Throughput Analytics Ingestion:** SDKs buffer metrics client-side. The API ingests telemetry into bounded `System.Threading.Channels` with `DropOldest` backpressure, flushing to PostgreSQL or horizontally scalable **Kafka + ClickHouse** clusters.
- 🔐 **Multi-Tenancy & RBAC:** Organization and Project-level isolation with strict Role-Based Access Control.
- 🔑 **Personal Access Tokens (PATs):** SHA-256 hashed PATs for secure CI/CD and CLI integrations.
- 🛡️ **SSRF-Protected Webhooks:** Secure outbound webhook dispatcher with Polly-powered exponential backoff and Dead-Letter Queues (DLQ).
- 📜 **Immutable Audit Logs:** EF Core `SaveChangesInterceptors` capture deep JSON diffs of every mutation.
- 💾 **Offline Resilience:** SDKs persist the latest synchronized state to a local JSON fallback file, ensuring safe boot-ups during complete network partitions.

---

## 🐳 Self-Hosting & Deployment

### Quick Start (PostgreSQL + Redis)

Deploying the core ToggleMesh stack takes under a minute.

```bash
git clone https://github.com/sdwck/ToggleMesh.git
cd ToggleMesh
cp .env.example .env    # Review and customize your secrets here
docker compose up -d
```

* **Admin UI:** `http://localhost:5173`
* **API:** `http://localhost:5264`
* **API Docs (Scalar):** `http://localhost:5264/docs`

### Enterprise Stack (+ Kafka & ClickHouse)

For production deployments requiring high-throughput analytics and horizontal OLAP scaling, boot the stack using the enterprise override:

```bash
docker compose -f docker-compose.yml -f docker-compose.enterprise.yml up -d
```

---

## 🤝 Contributing & License
ToggleMesh is released under the [MIT License](LICENSE).  
Contributions are welcome — please read our [Contributing Guidelines](CONTRIBUTING.md) before opening a PR.

---

<p align="center">
  <b>⭐ If ToggleMesh looks useful, star this repo — it helps others discover it.</b><br/>
  <a href="https://github.com/sdwck/ToggleMesh/issues">Report a Bug</a> · 
  <a href="https://github.com/sdwck/ToggleMesh/discussions">Request a Feature</a>
</p>
