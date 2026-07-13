import { describe, it, expect, beforeEach } from 'vitest';
import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import { ToggleMeshClient } from '../src/client.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

describe('Universal SDK Test Suite', () => {
    let client: ToggleMeshClient;
    let fixtures: any;

    beforeEach(() => {
        client = new ToggleMeshClient({
            serverKey: 'test-key',
            baseUrl: 'http://localhost'
        });
        
        (client as any).isRunning = true;
        
        const fixturePath = path.resolve(__dirname, '../../../tests/test-suite/evaluation-fixtures.json');
        fixtures = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));
    });

    it('should load fixtures correctly', () => {
        expect(fixtures.scenarios).toBeDefined();
        expect(fixtures.scenarios.length).toBeGreaterThan(0);
    });

    const fixturePath = path.resolve(__dirname, '../../../tests/test-suite/evaluation-fixtures.json');
    const fixturesData = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));

    for (const scenario of fixturesData.scenarios) {
        describe(scenario.name, () => {
            beforeEach(() => {
                (client as any).flagsCache.clear();
                (client as any).segmentsCache.clear();

                for (const flag of scenario.flags) {
                    const dto = { ...flag, originalDto: flag }; 
                    (client as any).cacheFlag(flag);
                }
            });

            for (const evalCase of scenario.evaluations) {
                it(`should evaluate flag '${evalCase.flagKey}' for identity '${evalCase.identity}'`, () => {
                    let result: any;
                    
                    if (evalCase.type === 'string') {
                        result = client.getStringValue(evalCase.flagKey, 'default', { identity: evalCase.identity, context: evalCase.context });
                        expect(result).toEqual(evalCase.expectedValue);
                    } else if (evalCase.type === 'boolean') {
                        result = client.isEnabled(evalCase.flagKey, false, { identity: evalCase.identity, context: evalCase.context });
                        expect(result).toEqual(evalCase.expectedValue === "true");
                    } else if (evalCase.type === 'number') {
                        result = client.getNumberValue(evalCase.flagKey, 0, { identity: evalCase.identity, context: evalCase.context });
                        expect(result).toEqual(Number(evalCase.expectedValue));
                    } else if (evalCase.type === 'json') {
                        result = client.getJsonValue(evalCase.flagKey, {}, { identity: evalCase.identity, context: evalCase.context });
                        expect(result).toEqual(JSON.parse(evalCase.expectedValue));
                    }
                });
            }
        });
    }
});
