import { create } from 'zustand';

export type RealTimeListener = (data: any) => void;

interface RealTimeState {
    connectionId: string | null;
    listeners: Map<string, Set<RealTimeListener>>;
    pendingSubscriptions: Set<string>;

    setConnectionId: (id: string) => void;
    subscribe: (topic: string, callback: RealTimeListener) => void;
    unsubscribe: (topic: string, callback: RealTimeListener) => void;
    dispatch: (topic: string, data: any) => void;
}

const sendSubscriptionRequest = async (connectionId: string, topic: string, action: 'subscribe' | 'unsubscribe') => {
    const token = localStorage.getItem('accessToken');
    if (!token) return;

    try {
        await fetch('/api/v1/realtime/subscriptions', {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ connectionId, topic, action })
        });
    } catch (err) {
        console.error(`Failed to ${action} to topic ${topic}`, err);
    }
};

export const useRealTimeStore = create<RealTimeState>((set, get) => ({
    connectionId: null,
    listeners: new Map(),
    pendingSubscriptions: new Set(),

    setConnectionId: (id: string) => {
        set({ connectionId: id });

        const state = get();
        Array.from(state.listeners.keys()).forEach(topic => {
            sendSubscriptionRequest(id, topic, 'subscribe');
        });

        state.pendingSubscriptions.forEach(topic => {
            sendSubscriptionRequest(id, topic, 'subscribe');
        });

        set({ pendingSubscriptions: new Set() });
    },

    subscribe: (topic: string, callback: RealTimeListener) => {
        const state = get();
        const newListeners = new Map(state.listeners);

        if (!newListeners.has(topic)) {
            newListeners.set(topic, new Set());

            if (state.connectionId) {
                sendSubscriptionRequest(state.connectionId, topic, 'subscribe');
            } else {
                const newPending = new Set(state.pendingSubscriptions);
                newPending.add(topic);
                set({ pendingSubscriptions: newPending });
            }
        }

        newListeners.get(topic)!.add(callback);
        set({ listeners: newListeners });
    },

    unsubscribe: (topic: string, callback: RealTimeListener) => {
        const state = get();
        const newListeners = new Map(state.listeners);
        const topicListeners = newListeners.get(topic);

        if (topicListeners) {
            topicListeners.delete(callback);

            if (topicListeners.size === 0) {
                newListeners.delete(topic);

                if (state.connectionId) {
                    sendSubscriptionRequest(state.connectionId, topic, 'unsubscribe');
                } else {
                    const newPending = new Set(state.pendingSubscriptions);
                    newPending.delete(topic);
                    set({ pendingSubscriptions: newPending });
                }
            }

            set({ listeners: newListeners });
        }
    },

    dispatch: (topic: string, data: any) => {
        const state = get();
        const topicListeners = state.listeners.get(topic);
        if (topicListeners) {
            topicListeners.forEach(cb => {
                try {
                    cb(data);
                } catch (e) {
                    console.error('Error in realtime listener:', e);
                }
            });
        }
    }
}));
