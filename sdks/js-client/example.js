import { ToggleMeshClient } from "./dist/index.js";

const API_KEY = process.env.TOGGLEMESH_API_KEY;
if (!API_KEY) {
    console.error("TOGGLEMESH_API_KEY environment variable is required.");
    process.exit(1);
}

const client = new ToggleMeshClient({
    baseUrl: "http://localhost:5264",
    clientKey: API_KEY,
    refreshInterval: 2
});

async function run() {
    console.log("JS-Client SDK started. Simulating traffic...");

    while (true) {
        const userId = "user_js_1";
        await client.identify(userId, { Browser: "Edge" });

        const variation = client.getVariation("mab-string-test", "default-variant");
        console.log(`[JS Client] Evaluated mab-string-test for ${userId}: ${variation}`);

        if (Math.random() > 0.3) {
            client.track("purchase", { sdk: "js-client" }, 15.0);
            console.log(`[JS Client] Tracked 'purchase' for ${userId}`);
        }

        await new Promise(r => setTimeout(r, 1500));
    }
}

run().catch(console.error);