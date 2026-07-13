import React, { createContext, useContext, useSyncExternalStore } from 'react';
import { ToggleMeshClient } from './index.js';

const ToggleMeshContext = createContext<ToggleMeshClient | null>(null);

interface ToggleMeshProviderProps {
    client: ToggleMeshClient;
    children: React.ReactNode;
}

export const ToggleMeshProvider: React.FC<ToggleMeshProviderProps> = ({ client, children }) => {
    return (
        <ToggleMeshContext.Provider value={client}>
            {children}
        </ToggleMeshContext.Provider>
    );
};

export const useToggleMeshClient = (): ToggleMeshClient => {
    const client = useContext(ToggleMeshContext);
    if (!client) {
        throw new Error('[ToggleMesh] useToggleMeshClient must be used within a ToggleMeshProvider.');
    }
    return client;
};

export const useFeatureFlag = (flagKey: string, defaultValue = false): boolean => {
    const client = useContext(ToggleMeshContext);

    if (!client) {
        console.warn('[ToggleMesh] useFeatureFlag must be used within a ToggleMeshProvider.');
        return defaultValue;
    }

    return useSyncExternalStore(
        (callback) => client.subscribe(callback),
        () => client.isEnabled(flagKey, defaultValue),
        () => defaultValue
    );
};

export const useFeatureFlagVariation = (flagKey: string, defaultValue = ""): string => {
    const client = useContext(ToggleMeshContext);

    if (!client) {
        console.warn('[ToggleMesh] useFeatureFlagVariation must be used within a ToggleMeshProvider.');
        return defaultValue;
    }

    return useSyncExternalStore(
        (callback) => client.subscribe(callback),
        () => client.getVariation(flagKey, defaultValue),
        () => defaultValue
    );
};