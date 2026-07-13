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
    server_key="YOUR_API_KEY"
))

# 1. Check a flag locally (Zero network latency!)
# Pass identity and context per-evaluation, since the server handles multiple users concurrently.
if client.is_enabled("new-feature", default_value=False, identity="user-123", tenant="sample_corp"):
    print("Feature is ON")
else:
    print("Feature is OFF")

# 2. Stop background threads when the app shuts down
client.stop()
```

## Supported Features
- Percentage Rollouts (Murmur3 / FNV-1a hashing)
- Operator matching (Strings, Numbers, Dates, SemVer, Regex)
- Real-time SSE updates
- Contextual Rollouts
- Segment Evaluation
- JSON Parsing `client.get_json(...)`
- Analytics Event Tracking (A/B testing conversions):
  ```python
  client.track("checkout_completed", value=150.0, identity="user-123", cart_size=3)
  ```

---

## 🚀 CLI Tool (Code Generation)

The `togglemesh` Python package also includes a powerful Command Line Interface for synchronizing feature flags from the control plane and generating strongly-typed Python classes. This eliminates typos and gives you full IDE autocomplete for your flag keys!

### 1. Setup Configuration
Run the configuration wizard to link the CLI to your ToggleMesh environment.
```bash
togglemesh config
```

### 2. Generate Typed Flags
Synchronize your flags and output a native Python file containing your flag constants:
```bash
togglemesh sync -l python -o ./flags.py
```

Now you can use the generated `Flags` class in your code:
```python
from flags import Flags
from togglemesh import ToggleMeshClient, ToggleMeshOptions

# ...
client.is_enabled(Flags.NEW_CHECKOUT_FLOW, default_value=False, identity="user-123")
```
