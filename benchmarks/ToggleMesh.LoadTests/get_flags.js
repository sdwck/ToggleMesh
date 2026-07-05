import http from 'k6/http';
import { check } from 'k6';
import { API_KEY, BASE_URL } from './config.js';

export const options = {
    stages: [
        { duration: '10s', target: 500 },
        { duration: '30s', target: 2000 },
        { duration: '10s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<30', 'p(99)<50'],
        http_req_failed: ['rate<0.01'],
    },
};

export default function () {
    const endpointUrl = `${BASE_URL}/api/v1/sdk/flags`;

    const requestParameters = {
        headers: {
            'x-api-key': API_KEY,
        },
    };

    const response = http.get(endpointUrl, requestParameters);

    check(response, {
        'is_ok_200': (r) => r.status === 200,
    });
}
