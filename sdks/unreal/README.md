# ToggleMesh Unreal Engine SDK

The official Unreal Engine SDK for ToggleMesh. This plugin provides a robust, production-ready integration with ToggleMesh for evaluating feature flags, running A/B experiments, and tracking analytics directly from Blueprints or C++.

## Installation

### Method 1: Source (Recommended)
1. Download or clone this repository.
2. Copy the `ToggleMesh` folder into your Unreal Engine project's `Plugins/` directory.
   *(If the `Plugins` folder doesn't exist in your project root, create it).*
3. Right-click your `.uproject` file and select **Generate Visual Studio project files**.
4. Compile your project. The plugin will be compiled along with your game code.

### Method 2: Pre-Packaged Binary
1. Download a pre-compiled ZIP release from the Releases page.
2. Extract the contents into your project's `Plugins/ToggleMesh/` directory.
3. Launch your project. The Engine will load the pre-compiled binaries automatically.

## Configuration

Before using the SDK, you must configure your API keys.
1. Open your Unreal Engine project.
2. Go to **Edit > Project Settings**.
3. Scroll down to the **Plugins** section and click on **ToggleMesh**.
4. Set the following fields:
   - **Base Url**: The URL of your ToggleMesh server (e.g. `https://api.togglemesh.dev`).
   - **Client Key**: Your ToggleMesh Client Key.
   - **Refresh Interval**: How often the SDK should synchronize flags and flush analytics buffers (in seconds). Default is 60.

## Usage (Blueprints)

The SDK exposes a global Subsystem called **ToggleMesh Subsystem**. You can access it from anywhere in your Blueprints by right-clicking and searching for "Get Toggle Mesh Subsystem".

### 1. Identify User
You must identify the user before checking flags. This initializes the session and fetches the flags from the server.
* Call **Identify** on the ToggleMesh Subsystem.
* Pass a unique `UserId` (e.g., Player ID, Epic ID, or a random GUID) and any context properties.
* *Note: If you do not call Identify, flags will return their default values.*

### 2. Evaluate Flags
* Call **Get Bool Flag** to evaluate a feature flag or experiment.
* Pass the `FlagKey` and a `DefaultValue` (used as a fallback if the network is down or the key is missing).
* **Analytics**: Every time you call this node, the SDK automatically increments a metric and logs an Exposure if the flag is part of an active experiment.

### 3. Track Custom Events
* Call **Track Event** to record a custom analytics event.
* Pass the `EventName`. 
* Click the down-arrow on the node to reveal the optional `Value` and `bHasValue` pins if you want to attach a numerical value (e.g., Revenue, Score) to the event.

## Analytics Buffering & Flushing

To prevent network starvation and maintain optimal game thread performance, the Unreal Engine SDK uses a **Local Buffering Architecture**.

* `GetBoolFlag` and `TrackEvent` do **not** block the game thread or spawn immediate HTTP requests. Instead, they write to local memory buffers.
* The SDK automatically flushes these buffers to the ToggleMesh server based on your **Refresh Interval** timer, and automatically during Engine shutdown.
* **Manual Flush**: If you want to force all pending analytics to be sent immediately (e.g., when a player finishes a level or exits to the main menu), you can call the **Flush Events** and **Flush Metrics** nodes manually.
