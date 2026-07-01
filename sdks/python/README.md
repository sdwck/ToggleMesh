# ToggleMesh Python SDK (Local Evaluation)

[![GitHub Repo](https://img.shields.io/badge/GitHub-ToggleMesh-blue?logo=github)](https://github.com/sdwck/ToggleMesh)

A lightweight, robust Python SDK for evaluating feature flags with ToggleMesh.

This SDK uses **Local Evaluation**. It connects to the ToggleMesh control plane once to fetch all rules, establishes an SSE (Server-Sent Events) connection to listen for updates, and evaluates all flags locally in-memory without making network requests. This ensures ultra-fast responses (sub-millisecond) suitable for high-throughput backend services.

## Installation

```bash
pip install togglemesh
```

## Usage

```python
import time
from togglemesh import ToggleMeshClient, ToggleMeshOptions

client = ToggleMeshClient(ToggleMeshOptions(
    base_url="http://localhost:5000",
    client_key="YOUR_API_KEY"
))

# 1. Identify the user
client.identify("user-123", {"tenant": "acme_corp", "plan": "enterprise"})

# 2. Check a flag locally (Zero network latency!)
if client.is_enabled("new-feature", default_value=False):
    print("Feature is ON")
else:
    print("Feature is OFF")

# 3. Stop background threads when the app shuts down
client.stop()
```

## Supported Features
- Percentage Rollouts (Murmur3 / FNV-1a hashing)
- Operator matching (Strings, Numbers, Dates, SemVer, Regex)
- Real-time SSE updates
- Contextual Rollouts
- Segment Evaluation
- Analytics Event Tracking (A/B testing conversions):
  ```python
  client.track("checkout_completed", properties={"cart_size": 3}, value=150.0, identity="user-123")
  ```
