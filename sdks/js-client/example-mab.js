import { ToggleMeshClient } from "./dist/index.js";
import crypto from "crypto";

const client = new ToggleMeshClient({ baseUrl: "http://localhost:5264", clientKey: process.env.TOGGLEMESH_API_KEY });

async function run() {
    console.log("JS-Client SDK MAB Simulation started (Edge -> First)");

    while (true) {
        for(let i=0; i<10; i++) {
            const userId = crypto.randomUUID();
            await client.identify(userId, { Browser: "Edge" });
            const variation = client.getVariation("mab-string-test", "default-variant");
            
            console.log(`[JS Client] Evaluated mab-string-test for ${userId}: ${variation}`);
            
            const prob = variation === "First" ? 0.155 : 0.145;
            if (Math.random() < prob) {
                client.track("purchase", { sdk: "js-client" }, 10.0);
            }
        }
        await new Promise(r => setTimeout(r, 1000));
    }
}

run();
