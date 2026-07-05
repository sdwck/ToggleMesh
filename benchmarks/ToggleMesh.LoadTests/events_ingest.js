import http from 'k6/http';
import { check, sleep } from 'k6';
import { API_KEY, BASE_URL } from './config.js';

export const options = {
    stages: [
        { duration: '10s', target: 500 },
        { duration: '30s', target: 2000 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<30', 'p(99)<50'],
        http_req_failed: ['rate<0.001'],
    },
};

export default function () {
    const endpointUrl = `${BASE_URL}/api/v1/sdk/events`;

    const payload = JSON.stringify({
        events: [
            {
                type: 0,
                timestamp: Date.now(),
                identity: `user_${__VU}`,
                flagKey: "load-test-flag",
                result: true
            },
            {
                type: 1,
                timestamp: Date.now(),
                identity: `user_${__VU}`,
                eventName: "purchase_completed",
                value: 99.99
            }
        ]
    });

    const requestParameters = {
        headers: {
            'Content-Type': 'application/json',
            'x-api-key': API_KEY,
        },
    };

    const response = http.post(endpointUrl, payload, requestParameters);

    check(response, {
        'is_accepted_202': (r) => r.status === 202,
    });
}