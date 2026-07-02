# ToggleMesh Node.js SDK

[![GitHub Repo](https://img.shields.io/badge/GitHub-ToggleMesh-blue?logo=github)](https://github.com/sdwck/ToggleMesh)

The official ToggleMesh Server-Side SDK for Node.js environments.
This SDK provides **Local Evaluation** capabilities by fetching rules from the ToggleMesh control plane and evaluating them securely in-memory, ensuring sub-millisecond evaluation latency for high-throughput backend services.

## 📦 Installation

```bash
npm install togglemesh-node
```

## 🛠️ Supported Features & Capabilities

- **Zero Network Latency Evaluation:** Rules are evaluated locally in-memory without blocking I/O.
- **Real-Time Synchronizations:** Subscribes to Server-Sent Events (SSE) to update flags instantly when changed.
- **Contextual Rollouts & Segments:** Full support for evaluating complex audience segments and A/B test splits.
- **Analytics Event Tracking:** Easily track experiment conversions and application telemetry:
  ```javascript
  client.track("signup_completed", { plan: "enterprise" }, 1.0, "user_123");
  ```

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.
