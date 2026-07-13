require("dotenv").config();
const axios = require("axios");
const { ToggleMeshClient } = require("togglemesh-node");

const BASE_URL = process.env.TOGGLEMESH_API_URL || "http://localhost:5264/api/v1";
const PAT = process.env.TOGGLEMESH_PAT;

if (!PAT) {
    console.error("Error: TOGGLEMESH_PAT environment variable is required.");
    process.exit(1);
}

const apiClient = axios.create({
    baseURL: BASE_URL,
    headers: {
        "x-pat-token": PAT,
        "Content-Type": "application/json"
    }
});

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

async function runHarness() {
    console.log("Starting ToggleMesh Node.js SDK Test Harness...");

    try {
        console.log("Creating Organization...");
        const orgRes = await apiClient.post("/organizations", { name: "E2E Test Org" });
        const orgId = orgRes.data.id;

        console.log("Creating Project...");
        const projRes = await apiClient.post("/projects", { name: "E2E Node SDK Test", organizationId: orgId });
        const projectId = projRes.data.id;
        console.log("Creating Environment...");
        const envRes = await apiClient.post(`/projects/${projectId}/environments`, { name: "Test Env" });
        const envId = envRes.data.id;

        console.log("Creating API Key...");
        const keyRes = await apiClient.post(`/projects/${projectId}/environments/${envId}/keys`, {
            name: "Server Key",
            keyType: 1
        });
        const serverKey = keyRes.data.plainKey;

        const crypto = require("crypto");
        console.log("Creating Multivariate Flag...");
        const varA = "variant-a";
        const varB = "variant-b";
        const varC = "variant-c";

        const varAId = crypto.randomUUID();
        const varBId = crypto.randomUUID();
        const varCId = crypto.randomUUID();

        await apiClient.post(`/projects/${projectId}/flags`, {
            key: "mab-test-flag",
            type: 1,
            tags: [],
            variations: [
                { id: varAId, value: varA },
                { id: varBId, value: varB },
                { id: varCId, value: varC }
            ]
        });

        const flagKey = "mab-test-flag";

        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/toggle`, {
            isEnabled: true
        });

        console.log("Creating Per-Rule Rollout...");
        await apiClient.put(`/projects/${projectId}/environments/${envId}/flags/${flagKey}`, {
            fallthroughRollout: [
                { variationId: varAId, weight: 3334 },
                { variationId: varBId, weight: 3333 },
                { variationId: varCId, weight: 3333 }
            ],
            rules: [
                {
                    groupId: 1,
                    attribute: "country",
                    operator: "equals",
                    value: "US",
                    rollout: [
                        { variationId: varAId, weight: 9000 },
                        { variationId: varBId, weight: 1000 },
                        { variationId: varCId, weight: 0 }
                    ]
                }
            ]
        });

        console.log("Starting MAB Experiment...");
        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/start`, {
            mode: "mab",
            goalEvent: "purchase",
            optimizationType: 0,
            contextPartitionKeys: [],
            mabExplorationFloor: 5,
            balanceWeights: true
        });

        console.log("Setup complete. Initializing SDK...");

        const client = new ToggleMeshClient({
            serverKey: serverKey,
            environmentId: envId,
            baseUrl: BASE_URL.replace("/api/v1", ""),
            refreshInterval: 1000
        });

        await client.start();
        console.log("SDK Initialized!");
        console.log(JSON.stringify(client.flagsCache.get(flagKey), null, 2));

        console.log("\n--- Scenario 1: Base Multivariate Distribution ---");
        const baseCounts = { [varA]: 0, [varB]: 0, [varC]: 0 };
        for (let i = 0; i < 3000; i++) {
            const val = client.getStringValue(flagKey, `user-${i}`, "default");
            if (baseCounts[val] !== undefined) baseCounts[val]++;
        }
        console.log(`Distribution (expected ~1000 each):`, baseCounts);

        console.log("\n--- Scenario 2: Per-Rule Rollouts (Country=US) ---");
        const usCounts = { [varA]: 0, [varB]: 0, [varC]: 0 };
        for (let i = 0; i < 3000; i++) {
            const val = client.getStringValue(flagKey, `us-user-${i}`, "default", { country: "US" });
            if (usCounts[val] !== undefined) usCounts[val]++;
        }
        console.log(`US Distribution (expected A:~2700, B:~300, C:0):`, usCounts);

        console.log("\n--- Scenario 3: MAB Traffic Shifting ---");
        console.log("Simulating traffic where Variant C has 80% conversion, A has 10%, B has 5%...");

        for (let i = 0; i < 5000; i++) {
            const userId = `mab-user-${i}`;
            const val = client.getStringValue(flagKey, userId, "default");

            let convRate = 0;
            if (val === varA) convRate = 0.10;
            else if (val === varB) convRate = 0.05;
            else if (val === varC) convRate = 0.80;

            if (Math.random() < convRate) {
                client.track("purchase", userId, { revenue: 100 }, 100);
            }
        }

        console.log("Waiting for events to flush...");
        await client.flushMetrics();
        while (client.eventsBuffer && client.eventsBuffer.length > 0) {
            await client.flushEvents();
        }
        await sleep(3000);

        console.log("Triggering Dev Endpoint (Force MAB iteration)...");
        try {
            await axios.post(`${BASE_URL}/dev/force-mab`);
        } catch (err) {
            console.error("Force MAB failed:", err.response ? err.response.data : err.message);
        }

        console.log("Waiting for SDK to fetch updated flags...");
        await sleep(2000);

        console.log("Running traffic again after MAB optimization...");
        const newCounts = { [varA]: 0, [varB]: 0, [varC]: 0 };
        for (let i = 5000; i < 8000; i++) {
            const val = client.getStringValue(flagKey, `mab-user-${i}`, "default");
            if (newCounts[val] !== undefined) newCounts[val]++;
        }
        console.log(`New Distribution (expected C to dominate):`, newCounts);

        console.log("\n--- Scenario 4: Contextual Bandits (Auto-segmentation) ---");
        const ctxFlagKey = "mab-context-flag";
        console.log("Creating second flag for Contextual Bandits...");
        const ctxVarAId = crypto.randomUUID();
        const ctxVarBId = crypto.randomUUID();

        await apiClient.post(`/projects/${projectId}/flags`, {
            key: ctxFlagKey,
            type: 1,
            tags: [],
            variations: [
                { id: ctxVarAId, value: varA },
                { id: ctxVarBId, value: varB }
            ]
        });

        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${ctxFlagKey}/toggle`, {
            isEnabled: true
        });

        console.log("Starting Contextual MAB Experiment (Partitioning by 'browser')...");
        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${ctxFlagKey}/experiments/start`, {
            mode: "mab",
            goalEvent: "signup",
            optimizationType: 0,
            contextPartitionKeys: ["browser"],
            mabExplorationFloor: 5,
            balanceWeights: true
        });

        console.log("Waiting for SDK to fetch new flag...");
        await sleep(2000);

        console.log("Simulating contextual traffic...");
        for (let i = 0; i < 4000; i++) {
            const browser = i % 2 === 0 ? "Chrome" : "Safari";
            const userId = `ctx-user-${i}`;
            const val = client.getStringValue(ctxFlagKey, userId, "default", { browser });

            let convRate = 0.05;
            if (browser === "Chrome" && val === varA) convRate = 0.80;
            if (browser === "Safari" && val === varB) convRate = 0.80;

            if (Math.random() < convRate) {
                client.track("signup", userId, { browser });
            }
        }

        console.log("Waiting for events to flush...");
        await client.flushMetrics();
        while (client.eventsBuffer && client.eventsBuffer.length > 0) {
            await client.flushEvents();
        }
        await sleep(3000);

        console.log("Triggering Dev Endpoint (Force MAB iteration)...");
        try { await axios.post(`${BASE_URL}/dev/force-mab`); } catch (err) { }

        console.log("Waiting for SDK to fetch contextual rules...");
        await sleep(2000);

        console.log("Validating Contextual Bandits routing...");
        const chromeCounts = { [varA]: 0, [varB]: 0 };
        const safariCounts = { [varA]: 0, [varB]: 0 };

        for (let i = 4000; i < 6000; i++) {
            const browser = i % 2 === 0 ? "Chrome" : "Safari";
            const val = client.getStringValue(ctxFlagKey, `ctx-user-${i}`, "default", { browser });
            if (browser === "Chrome" && chromeCounts[val] !== undefined) chromeCounts[val]++;
            if (browser === "Safari" && safariCounts[val] !== undefined) safariCounts[val]++;
        }

        console.log("\n--- Scenario 5: Edge Cases and Data Types ---");
        const boolFlagKey = "bool-flag";
        const boolVarTrue = crypto.randomUUID();
        const boolVarFalse = crypto.randomUUID();
        await apiClient.post(`/projects/${projectId}/flags`, {
            key: boolFlagKey,
            type: 0,
            tags: [],
            variations: [
                { id: boolVarTrue, value: "true" },
                { id: boolVarFalse, value: "false" }
            ]
        });
        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${boolFlagKey}/toggle`, { isEnabled: true });

        const numFlagKey = "num-flag";
        const numVar42 = crypto.randomUUID();
        const numVar100 = crypto.randomUUID();
        await apiClient.post(`/projects/${projectId}/flags`, {
            key: numFlagKey,
            type: 2,
            tags: [],
            variations: [
                { id: numVar42, value: "42" },
                { id: numVar100, value: "100" }
            ]
        });
        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${numFlagKey}/toggle`, { isEnabled: true });

        const jsonFlagKey = "json-flag";
        const jsonVar1 = crypto.randomUUID();
        const jsonVar2 = crypto.randomUUID();
        await apiClient.post(`/projects/${projectId}/flags`, {
            key: jsonFlagKey,
            type: 3,
            tags: [],
            variations: [
                { id: jsonVar1, value: "{\"theme\":\"dark\"}" },
                { id: jsonVar2, value: "{\"theme\":\"light\"}" }
            ]
        });
        await apiClient.post(`/projects/${projectId}/environments/${envId}/flags/${jsonFlagKey}/toggle`, { isEnabled: true });

        const disabledFlagKey = "disabled-flag";
        const disVarOn = crypto.randomUUID();
        const disVarOff = crypto.randomUUID();
        await apiClient.post(`/projects/${projectId}/flags`, {
            key: disabledFlagKey,
            type: 1,
            tags: [],
            variations: [
                { id: disVarOn, value: "on" },
                { id: disVarOff, value: "off" }
            ],
            offVariationId: disVarOff
        });

        console.log("Waiting for SDK to fetch new flags...");
        await sleep(2000);

        console.log("Testing Boolean value:", client.getBooleanValue(boolFlagKey, "user-1", false));
        console.log("Testing Number value:", client.getNumberValue(numFlagKey, "user-1", 0));
        console.log("Testing JSON value:", client.getJsonValue(jsonFlagKey, "user-1", {}));

        console.log("Testing Disabled flag with OffVariation:", client.getStringValue(disabledFlagKey, "user-1", "default"));
        console.log("Testing Missing flag:", client.getStringValue("non-existent-flag", "user-1", "default"));
        console.log("Testing Null Identity:", client.getStringValue(flagKey, null, "default"));

        console.log("\nCleaning up Organization...");
        await apiClient.delete(`/organizations/${orgId}`);
        console.log("Cleanup complete!");

        console.log("\nTest Harness completed successfully!");
        process.exit(0);

    } catch (error) {
        console.error("Error in Test Harness:");
        if (error.response) {
            console.error(error.response.status, error.response.data);
        } else {
            console.error(error.message);
        }
        process.exit(1);
    }
}

runHarness();
