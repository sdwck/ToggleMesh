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
    const endpointUrl = `${BASE_URL}/api/v1/sdk/metrics`;

    const payload = JSON.stringify([
        {
            key: "load-test-flag",
            trueCount: Math.floor(Math.random() * 10) + 1,
            falseCount: Math.floor(Math.random() * 5)
        }
    ]);

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