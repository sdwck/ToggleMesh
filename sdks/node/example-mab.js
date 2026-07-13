import { ToggleMeshClient } from "./dist/index.js";
import crypto from "crypto";

const client = new ToggleMeshClient({ baseUrl: "http://localhost:5264", serverKey: process.env.TOGGLEMESH_API_KEY });

async function run() {
    await client.start();
    console.log("Node SDK MAB Simulation started (Chrome -> First)");

    setInterval(() => {
        for(let i=0; i<10; i++) {
            const userId = crypto.randomUUID();
            const variation = client.getStringValue("mab-test-flag", "control", { identity: userId });
            console.log(`[${userId}] Evaluated mab-test-flag: ${variation}`);

            if (variation === "treatment") {
                if (Math.random() < 0.3) {
                    console.log(`[${userId}] Tracking conversion!`);
                    client.track("mab_conversion", {
                        identity: userId,
                        context: { source: "example-mab" },
                        value: 10.0
                    });
                }
            } else {
                if (Math.random() < 0.1) {
                    console.log(`[${userId}] Tracking conversion!`);
                    client.track("mab_conversion", {
                        identity: userId,
                        context: { source: "example-mab" },
                        value: 10.0
                    });
                }
            }
        }
    }, 1000);
}

run();
