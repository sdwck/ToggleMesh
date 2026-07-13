import { ToggleMeshClient } from "./dist/index.js";

const API_KEY = process.env.TOGGLEMESH_API_KEY;
if (!API_KEY) {
    console.error("TOGGLEMESH_API_KEY environment variable is required.");
    process.exit(1);
}

const client = new ToggleMeshClient({
    baseUrl: "http://localhost:5264",
    serverKey: API_KEY
});

async function run() {
    await client.start();
    console.log("Node SDK started. Simulating traffic...");

    while (true) {
        const userId = "user_node_1";
        const uid = userId;

        const variation = client.getStringValue("mab-string-test", "default-variant", {
            identity: uid,
            context: { Browser: "Safari" }
        });
        console.log(`[Node SDK] Evaluated mab-string-test for ${uid}: ${variation}`);

        if (Date.now() % 3 === 0) {
            client.track("purchase", {
                identity: uid,
                context: { sdk: "node" },
                value: 15.0
            });
            console.log(`[Node SDK] Tracked 'purchase' for ${userId}`);
        }

        await new Promise(r => setTimeout(r, 1500));
    }
}

run().catch(console.error);
