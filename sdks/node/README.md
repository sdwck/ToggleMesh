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
  ```javascript
  const isEnabled = client.isEnabled("new-feature", false, { identity: "user_123", context: { plan: "enterprise" } });
  
  // You can also evaluate typed configurations:
  const color = client.getStringValue("button-color", "blue", { identity: "user_123" });
  const config = client.getJsonValue("ui-config", { header: "default" }, { identity: "user_123" });
  ```
- **Analytics Event Tracking:** Easily track experiment conversions and application telemetry:
  ```javascript
  client.track("signup_completed", { context: { plan: "enterprise" }, value: 1.0, identity: "user_123" });
  ```

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.
