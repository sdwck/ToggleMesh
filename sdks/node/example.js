import { ToggleMeshClient } from './dist/index.js';

const client = new ToggleMeshClient({
    baseUrl: 'http://localhost:5264',
    serverKey: 'tm_server_w9pYCQFCJj3DdXuzsQfWvZNSIjE1x0zrMSv5PHvrW8'
});

async function main() {
    await client.start();
    console.log("Starting flag evaluation loop (press Ctrl+C to exit)...");

    setInterval(() => {
        const email = "nirawolker@gmail.com";
        const uid = "123456";
        const context = { Email: email, UserId: uid };

        const isEnabled = client.isEnabled("gmail-20percent", { identity: uid, context });

        if (isEnabled) {
            const eventProperties = { loadTimeMs: 120, buttonColor: "red" };
            client.track("node_gmail_checked", { identity: uid, context }, eventProperties, 1.0);
            console.log(`[Gmail 20%] ${email} -> ENABLED!`);
        } else {
            console.log(`[Gmail 20%] ${email} -> DISABLED.`);
        }
    }, 3000);
}

main().catch(console.error);

process.on('SIGINT', () => {
    console.log("Stopping...");
    client.stop();
    process.exit(0);
});
