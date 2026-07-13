# 🔌 ToggleMesh.SDK

[![GitHub Repo](https://img.shields.io/badge/GitHub-ToggleMesh-blue?logo=github)](https://github.com/sdwck/ToggleMesh)

The official, ultra-high-performance C# Client SDK for **ToggleMesh** — a real-time, self-hosted feature flag and configuration management engine.

ToggleMesh SDK is engineered for **hardcore, high-throughput backend services**. By utilizing modern .NET features, it achieves sub-40ns evaluation latency with **zero heap allocations** during flag evaluation, entirely eliminating Garbage Collection overhead on your hot paths.

## 🚀 Performance Metrics

*   **Evaluation Latency:** **<30 nanoseconds** (Mean) on standard hardware depending on context structure.
*   **Memory Allocations:** **0 Bytes** (Zero managed allocations per check, including complex string matching and segment evaluations).
*   **Synchronization:** Real-time push via SignalR over Redis backplane (no polling).

*Benchmarks executed using [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) on .NET 10.0 / Intel Core i7-14700K:*
| Method | Mean | StdDev | Max | P95 | Allocated |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Evaluate_NoRules_AOT** *(Baseline)* | **7.43 ns** | 0.04 ns | 7.50 ns | 7.48 ns | **-** |
| **Evaluate_1Rule_AOT** *(Typical)* | **29.53 ns** | 0.11 ns | 29.67 ns | 29.66 ns | **-** |
| **Evaluate_ComplexRule_AOT** *(MAB/Rollout)* | **96.66 ns** | 0.48 ns | 97.69 ns | 97.47 ns | **-** |
| **Evaluate_10Rules_AOT** *(Worst-case)* | **114.23 ns** | 0.38 ns | 114.94 ns | 114.76 ns | **-** |
| **TrackEvent_10Rules_AOT** *(Metrics Buffer)*| **45.84 ns** | 0.06 ns | 45.98 ns | 45.93 ns | **-** |

---

## 📦 Installation

Install the package via NuGet CLI:

```bash
dotnet add package ToggleMesh.SDK
```
Or via the NuGet Package Manager in your IDE.

---

## ⚡ Quick Start

### 1. Register the Client (`Program.cs`)
Register ToggleMesh in your Dependency Injection container. To enable automatic context resolution from ASP.NET Core HTTP requests, register the HttpContext provider:

```csharp
using ToggleMesh.SDK.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Register ToggleMesh SDK
builder.Services.AddToggleMeshClient(options =>
{
    options.BaseUrl = "https://api.togglemesh.dev"; // Your ToggleMesh control plane address
    options.ApiKey = "tm_server_xxxxxxxxxxxxxxxxxxxxxxxx"; // Your environment API key
});

// Optional: Enable automatic context mapping from HttpContext (Claims, Roles, etc.)
builder.Services.AddToggleMeshHttpContext();
```

### 2. Evaluate Flags in Your Business Logic
Inject `IToggleMeshClient` into any of your services. The SDK automatically resolves the current user's ID, Email, and Roles from the ambient `HttpContext` on every check, evaluating targeting rules in-memory instantly.

```csharp
using ToggleMesh.SDK.Clients;

public class CheckoutService
{
    private readonly IToggleMeshClient _toggleMesh;

    public CheckoutService(IToggleMeshClient toggleMesh)
    {
        _toggleMesh = toggleMesh;
    }

    public void CompletePurchase()
    {
        // Falls back to defaultValue (true) if the API is down and no cache exists.
        if (_toggleMesh.IsEnabled("new-checkout-flow", defaultValue: true))
        {
            // ...
        }
    }
}
```

---

## 🛡️ Resilience & Fail-Safe Architecture

ToggleMesh SDK is designed to keep your application alive no matter what:

1.  **Non-Blocking Startup:** The SDK connects asynchronously in the background. If the ToggleMesh server is offline, your application boots up instantly using offline fallbacks.
2.  **Polly Integration:** Under the hood, the SDK utilizes **Polly** Resilience Pipelines. It features a configured **Circuit Breaker** and Exponential Backoff Retries to protect your services and prevent self-DDoS during outages.
3.  **Local Fallback Cache:** If configured, the SDK writes the latest valid configuration to a local `.togglemesh/` JSON file. On startup, if the API is unreachable, it seamlessly loads from this file.

---

## 🛠️ Supported Features & Capabilities

- **Zero-Allocation Rule Evaluation:** Advanced caching and optimization ensures GC-free evaluation.
- **AOT Context Generation:** Uses Source Generators (`[ToggleMeshContext]`) to eliminate reflection when resolving user context.
- **Contextual Rollouts & Experiments:** First-class support for A/B testing and contextual targeting.
- **Segment Evaluation:** Evaluate flags against synchronized audience segments instantly.
- **Analytics Event Tracking:** Send custom telemetry and experiment conversions securely and asynchronously:
  ```csharp
  _toggleMesh.Track("checkout_completed", value: 120.50);
  ```

---

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.
