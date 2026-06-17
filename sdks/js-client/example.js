import { ToggleMeshClient } from './dist/index.js';

async function run() {
    const client = new ToggleMeshClient({
        baseUrl: 'http://localhost:5264',
        clientKey: 'tm_client_IKDPqZdiPsU44xEL2WyLla2fTSCTN8HS0WO0sVRKgY'
    });

    await client.identify('user_123', { Country: 'US', Plan: 'Pro' });

    const isNewCheckout = client.isEnabled('new-checkout-flow');
    console.log(`'new-checkout-flow' enabled status: ${isNewCheckout}`);

    const defaultFlag = client.isEnabled('some-non-existent-flag', true);
    console.log(`'some-non-existent-flag' (fallback to default): ${defaultFlag}`);
}

run().catch(console.error);