# 🔌 togglemesh-js

[![GitHub Repo](https://img.shields.io/badge/GitHub-ToggleMesh-blue?logo=github)](https://github.com/sdwck/ToggleMesh)

Official, lightweight, and reactive JavaScript/TypeScript Client SDK for **ToggleMesh** — the real-time, high-performance feature flag and configuration management engine.

This SDK is specifically designed for **Client-Side (Browser, Mobile/React Native) environments**, leveraging secure **Remote Evaluation** to evaluate targeting rules on the server side, preventing sensitive rule leakage to the client.

## ✨ Features

*   **Zero Dependencies:** Designed on top of native `fetch` API, keeping your bundle size tiny (~2KB).
*   **Reactive Hooks:** Out-of-the-box React Context Provider and hooks that trigger automatic re-renders when flags update.
*   **Automatic Background Polling:** SDK automatically syncs flag states with the API in the background.
*   **Analytics Event Tracking:** Send custom telemetry and experiment conversions seamlessly:
    ```javascript
    tmClient.track('button_clicked', { color: 'red' });
    ```
*   **Dual-Module Export:** Full support for ESM (`import`) and TypeScript definitions.
*   **Zero Client-Side Rule Leakage:** All targeting rules are evaluated secure-side on the ToggleMesh server, returning only flat boolean results to the client.

---

## 📦 Installation

Install the package via your preferred package manager:

```bash
npm install togglemesh-js
# or
yarn add togglemesh-js
# or
pnpm add togglemesh-js
```

---

## 🚀 Quick Start (React / TypeScript)

### 1. Initialize and Wrap Your App
Initialize the client with your **Client API Key** (starts with `tm_client_`) and wrap your app in `ToggleMeshProvider`. Trigger the initial user identification:

```tsx
import React, { useEffect } from 'react';
import ReactDOM from 'react-dom/client';
import { ToggleMeshClient } from 'togglemesh-js';
import { ToggleMeshProvider } from 'togglemesh-js/react';
import { App } from './App';

// Initialize the Singleton Client
const tmClient = new ToggleMeshClient({
  baseUrl: 'https://api.togglemesh.dev',
  clientKey: 'tm_client_your_api_key_here',
  refreshInterval: 30 // Background sync interval in seconds (default: 60)
});

function Root() {
  useEffect(() => {
    // Identify the user with context properties. 
    // This triggers the initial remote evaluation.
    tmClient.identify('user_123', {
      Country: 'US',
      Plan: 'Pro',
      DeviceType: 'Mobile'
    });
  }, []);

  return (
    <ToggleMeshProvider client={tmClient}>
      <App />
    </ToggleMeshProvider>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(<Root />);
```

### 2. Use the Reactive Hook in Components
Use the `useFeatureFlag` hook inside any nested component. It automatically subscribes to cache changes and re-renders if a flag is updated:

```tsx
import React from 'react';
import { useFeatureFlag } from 'togglemesh-js/react';

export function CheckoutPage() {
  // Evaluates instantly from in-memory cache. 
  // Falls back to defaultValue (false) if offline.
  const showPaypal = useFeatureFlag('new-checkout-flow');

  return (
    <div className="payment-container">
      <CreditCardForm />
      {showPaypal && <PayPalButton />}
    </div>
  );
}
```

---

## 💻 Vanilla JavaScript / Node.js Usage

If you are not using React, you can import the raw client and handle evaluations manually:

```javascript
import { ToggleMeshClient } from 'togglemesh-js';

const client = new ToggleMeshClient({
  baseUrl: 'https://api.togglemesh.dev',
  clientKey: 'tm_client_xxxx'
});

// Identify session and fetch flags
await client.identify('anonymous_user', { Country: 'US' });

// Synchronous evaluation (0ms latency)
const isEnabled = client.isEnabled('beta-feature', true);
```

You can also subscribe to raw configuration updates:

```javascript
const unsubscribe = client.subscribe((flags) => {
  console.log("Flags updated in memory:", flags);
});

// Call unsubscribe() when you want to clean up the listener
```

---

## 🔒 Security & Privacy (Why Remote Evaluation?)

Unlike server-side SDKs, `togglemesh-js` uses **Remote Evaluation**. 

Your raw targeting rules (e.g., `If Email ends with @competitor.com`) are **never** downloaded to the browser. The browser only sends the user's `identity` and `context` payload, and the ToggleMesh server evaluates the rules secure-side. The browser receives a simple `JSON` response containing only the authorized, public flags and their boolean outcomes, completely protecting your business logic.

---

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.