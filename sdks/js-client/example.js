import { ToggleMeshClient } from './dist/index.js';

async function run() {
    const client = new ToggleMeshClient({
        baseUrl: 'http://localhost:5264',
        clientKey: 'tm_client_xQJJGFTZrEvXW6nVbisQmm2eWDuoQuF6PkjyPIkZm0',
        refreshInterval: 5
    });

    await client.identify('user-123', { Country: 'US', Plan: 'Pro', Email: 'nirawolker@gmail.com' });
    console.log("Starting flag evaluation loop (press Ctrl+C to exit)...");

    setInterval(() => {
        const isGmail20Percent = client.isEnabled('gmail-20percent');

        if (isGmail20Percent) {
            client.track("js_client_gmail_checked", { loadTime: 120 });
            console.log(`'gmail-20percent' enabled status: ENABLED!`);
        } else {
            console.log(`'gmail-20percent' enabled status: DISABLED.`);
        }
    }, 3000);

    process.on('SIGINT', () => {
        console.log("Stopping...");
        client.clearIdentity();
        process.exit(0);
    });
}

run().catch(console.error);