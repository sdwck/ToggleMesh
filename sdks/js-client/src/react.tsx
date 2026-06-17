import React, { createContext, useContext, useEffect, useState } from 'react';
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

export const useFeatureFlag = (flagKey: string, defaultValue = false): boolean => {
    const client = useContext(ToggleMeshContext);

    if (!client) {
        console.warn('[ToggleMesh] useFeatureFlag must be used within a ToggleMeshProvider.');
        return defaultValue;
    }

    const [isEnabled, setIsEnabled] = useState(() => client.isEnabled(flagKey, defaultValue));

    useEffect(() => {
        const unsubscribe = client.subscribe(() => {
            setIsEnabled(client.isEnabled(flagKey, defaultValue));
        });

        setIsEnabled(client.isEnabled(flagKey, defaultValue));

        return unsubscribe;
    }, [client, flagKey, defaultValue]);

    return isEnabled;
};