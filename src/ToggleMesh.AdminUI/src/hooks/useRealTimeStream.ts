import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useRealTimeStore } from '../stores/useRealTimeStore';
import { fetchEventSource } from '@microsoft/fetch-event-source';

import api from '../api/axios';

export function useRealTimeStream() {
    const queryClient = useQueryClient();
    const abortRef = useRef<AbortController | null>(null);

    useEffect(() => {
        let cancelled = false;

        async function connect() {
            const token = localStorage.getItem('accessToken');
            if (!token || cancelled) return;

            const controller = new AbortController();
            abortRef.current = controller;

            try {
                await fetchEventSource('/api/v1/realtime/stream', {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Accept': 'text/event-stream',
                    },
                    signal: controller.signal,
                    async onopen(response) {
                        if (response.status === 401) {
                            try {
                                await api.get('/user/profile');
                            } catch {
                                throw new Error('Token refresh failed');
                            }
                            throw new Error('SSE stream returned 401, retrying with new token');
                        }
                    },
                    onmessage(ev) {
                        if (ev.event === 'connected' && ev.data) {
                            try {
                                const parsed = JSON.parse(ev.data);
                                if (parsed?.connectionId) {
                                    useRealTimeStore.getState().setConnectionId(parsed.connectionId);
                                }
                            } catch { }
                        } else if (ev.event === 'invalidate' && ev.data) {
                            try {
                                const parsed = JSON.parse(ev.data);
                                if (parsed?.queryKey) {
                                    queryClient.invalidateQueries({ queryKey: parsed.queryKey });
                                }
                            } catch { }
                        } else if (ev.event && ev.data) {
                            try {
                                const parsed = JSON.parse(ev.data);
                                useRealTimeStore.getState().dispatch(ev.event, parsed);
                            } catch { }
                        }
                    },
                    onerror(err) {
                        console.warn('SSE connection error, retrying...', err);
                    }
                });
            } catch (err: unknown) {
                if (cancelled || (err instanceof DOMException && err.name === 'AbortError')) {
                    return;
                }
                console.error('SSE connection failed fatally', err);
            }
        }

        connect();

        return () => {
            cancelled = true;
            abortRef.current?.abort();
        };
    }, [queryClient]);
}
