<div align="center">
  <img src="src/ToggleMesh.AdminUI/src/assets/icon.png" alt="ToggleMesh Logo" width="120" />
  <h1>ToggleMesh</h1>
  <p><b>Enterprise Feature Flag & Contextual Experimentation Engine</b></p>
  <p>A high-performance, data-private, self-hosted alternative to LaunchDarkly and Statsig. Powered by .NET Core, featuring native SDKs for C#, Python, Go, Node.js, and C++.</p>
</div>

<p align="center">
  <a href="https://github.com/sdwck/ToggleMesh/actions/workflows/publish_sdk.yml">
    <img src="https://img.shields.io/github/actions/workflow/status/sdwck/ToggleMesh/publish_sdk.yml?style=for-the-badge&logo=github&label=Build" alt="Build Status" />
  </a>
  <a href="https://www.nuget.org/packages/ToggleMesh.SDK">
    <img src="https://img.shields.io/nuget/v/ToggleMesh.SDK?style=for-the-badge&logo=nuget&logoColor=white&color=512BD4&label=NuGet" alt="NuGet Package" />
  </a>
  <a href="https://www.npmjs.com/package/togglemesh-js">
    <img src="https://img.shields.io/npm/v/togglemesh-js?style=for-the-badge&logo=npm&logoColor=white&color=CB3837&label=npm%20js" alt="npm JS Package" />
  </a>
  <a href="https://www.npmjs.com/package/togglemesh-node">
    <img src="https://img.shields.io/npm/v/togglemesh-node?style=for-the-badge&logo=nodedotjs&logoColor=white&color=5FA04E&label=Node.js" alt="npm Node Package" />
  </a>
  <a href="https://pypi.org/project/togglemesh/">
    <img src="https://img.shields.io/pypi/v/togglemesh?style=for-the-badge&logo=python&logoColor=white&color=3776AB&label=PyPI" alt="PyPI Package" />
  </a>
  <br />
  <a href="#performance">
    <img src="https://img.shields.io/badge/Latency-%3C30ns-success?style=for-the-badge" alt="<30ns Latency" />
  </a>
  <a href="#performance">
    <img src="https://img.shields.io/badge/Allocations-0_Bytes-0078D4?style=for-the-badge" alt="0 Bytes Allocated" />
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-MIT-2ea44f?style=for-the-badge" alt="MIT License" />
  </a>
</p>

*![ToggleMesh Admin Dashboard](docs/assets/dashboard-preview.png)*  
*Manage environments, targeting rules, and A/B tests from a unified, modern interface.*

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

## 🏗️ Architecture

```mermaid
flowchart TD
    classDef ui fill:#18181b,stroke:#3f3f46,stroke-width:2px,color:#f4f4f5;
    classDef api fill:#0f172a,stroke:#6366f1,stroke-width:2px,color:#f4f4f5;
    classDef db fill:#312e81,stroke:#818cf8,stroke-width:2px,color:#f4f4f5;
    classDef cache fill:#7f1d1d,stroke:#f87171,stroke-width:2px,color:#f4f4f5;
    classDef sdk fill:#064e3b,stroke:#10b981,stroke-width:2px,color:#f4f4f5;
    classDef worker fill:#4c1d95,stroke:#a5b4fc,stroke-width:2px,color:#f4f4f5;

    UI[React Admin UI]:::ui -- REST / JWT --> API["ToggleMesh.API<br/>(Management & SSE)"]:::api
    
    API -- Read / Write --> PG[(PostgreSQL)]:::db
    API -- EF Interceptor Publish --> REDIS[(Redis Pub/Sub)]:::cache
    REDIS -. Fan-out Invalidate .-> API
    
    API == SSE Push ==> SDK["Client & Server SDKs<br/>(Zero-Alloc Eval)"]:::sdk

    SDK -- Async Batch Events --> INGEST["ToggleMesh.API<br/>(Ingest Endpoint)"]:::api

    subgraph Analytics Pipeline
        direction LR
        INGEST -- Stream --> KAFKA[(Kafka / Channels)]:::db
        KAFKA -- Consume --> CH[(ClickHouse OLAP)]:::db
        CH -- Bayesian Query --> WORKER[MAB Rollup Worker]:::worker
    end

    WORKER -. Auto-adjust traffic .-> PG
```
*Control Plane mutates state -> Interceptor triggers Redis Pub/Sub -> API fans out SSE to connected SDKs.*

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
    options.BaseUrl = "https://api.togglemesh.dev"; // Your self-hosted Control Plane
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

## ⚡ Performance & Benchmarks

ToggleMesh is engineered for ultra-low-latency microservices where Garbage Collection (GC) pauses are unacceptable.

By leveraging compiled Expression Trees, pre-computed rule groups, C# Source Generators, and `readonly ref struct` contexts, the ToggleMesh SDK achieves **zero-allocation evaluation**.

### BenchmarkDotNet Results
*We benchmarked various scenarios: from a simple global toggle to a worst-case scenario evaluating 10 nested AND/OR targeting rules.*

| Method | Mean | StdDev | Max | P95 | Allocated |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Evaluate_NoRules_AOT** *(Baseline)* | **7.28 ns** | 0.03 ns | 7.34 ns | 7.33 ns | **-** |
| **Evaluate_1Rule_AOT** *(Typical)* | **29.84 ns** | 0.72 ns | 30.73 ns | 30.65 ns | **-** |
| **Evaluate_ComplexRule_AOT** *(MAB/Rollout)* | **94.68 ns** | 0.15 ns | 94.94 ns | 94.87 ns | **-** |
| **Evaluate_10Rules_AOT** *(Worst-case)* | **117.11 ns** | 1.08 ns | 118.85 ns | 118.71 ns | **-** |
| **TrackEvent_10Rules_AOT** *(Metrics Buffer)*| **46.43 ns** | 0.13 ns | 46.66 ns | 46.63 ns | **-** |

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
- 🎯 **Advanced Targeting Engine:** Individual user overrides, Contextual Percentage Rollouts, and Semantic Versioning (SemVer) operators synchronized across all supported SDKs.
- 🎛️ **Multivariate Flags & Remote Config:** Move beyond simple booleans. Serve strongly-typed JSON, strings, or numeric payloads dynamically to your clients, enabling complex UI theming, game balancing, and multi-variant A/B/C testing without deploying new code.
- 🧠 **Contextual Multi-Armed Bandits (MAB):** Built-in Bayesian inference engine (Monte Carlo simulations via Beta distributions). Autonomously shifts traffic toward winning variants based on conversion or revenue metrics.
- 🔬 **Sample Ratio Mismatch (SRM) Detection:** Automated background statistical checks (Chi-Square) to detect tracking bugs or critical assignment skews in your A/B tests before they ruin your data.
- 📈 **High-Throughput Analytics Ingestion:** SDKs buffer metrics client-side. The API ingests telemetry into bounded `System.Threading.Channels` with `DropOldest` backpressure, flushing to PostgreSQL or horizontally scalable **Kafka + ClickHouse** clusters.
- 🔌 **Integrations & Webhooks:** Native Slack and MS Teams notifications, plus SSRF-Protected outbound webhook dispatcher with Polly-powered exponential backoff and Dead-Letter Queues (DLQ).
- 🔐 **Multi-Tenancy & RBAC:** Organization and Project-level isolation with strict Role-Based Access Control.
- 🔑 **Personal Access Tokens (PATs):** SHA-256 hashed PATs for secure CI/CD and CLI integrations.
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
