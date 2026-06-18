import { useEffect, useRef } from 'react';
import { useQueryClient } from '@tanstack/react-query';

const BASE_RETRY_MS = 1_000;
const MAX_RETRY_MS = 30_000;

export function useRealTimeStream() {
    const queryClient = useQueryClient();
    const abortRef = useRef<AbortController | null>(null);

    useEffect(() => {
        let retryCount = 0;
        let cancelled = false;

        async function connect() {
            const token = localStorage.getItem('accessToken');
            if (!token || cancelled) return;

            const controller = new AbortController();
            abortRef.current = controller;

            try {
                const response = await fetch('/api/v1/realtime/stream', {
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'Accept': 'text/event-stream',
                    },
                    signal: controller.signal,
                });

                if (!response.ok) {
                    throw new Error(`SSE stream returned ${response.status}`);
                }

                const reader = response.body!.getReader();
                const decoder = new TextDecoder();
                let buffer = '';
                let currentEvent = '';
                let currentData = '';

                retryCount = 0;

                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;

                    buffer += decoder.decode(value, { stream: true });

                    const lines = buffer.split('\n');
                    buffer = lines.pop()!;

                    for (const line of lines) {
                        if (line.startsWith('event: ')) {
                            currentEvent = line.slice(7).trim();
                        } else if (line.startsWith('data: ')) {
                            currentData = line.slice(6).trim();
                        } else if (line.trim() === '') {
                            if (currentEvent === 'invalidate' && currentData) {
                                try {
                                    const parsed = JSON.parse(currentData);
                                    if (parsed?.queryKey) {
                                        queryClient.invalidateQueries({ queryKey: parsed.queryKey });
                                    }
                                } catch { }
                            }
                            currentEvent = '';
                            currentData = '';
                        }
                    }
                }
            } catch (err: unknown) {
                if (cancelled || (err instanceof DOMException && err.name === 'AbortError')) {
                    return;
                }
            }

            if (cancelled) return;

            const delay = Math.min(BASE_RETRY_MS * Math.pow(2, retryCount), MAX_RETRY_MS);
            retryCount++;
            await new Promise(resolve => setTimeout(resolve, delay));

            if (!cancelled) {
                connect();
            }
        }

        connect();

        return () => {
            cancelled = true;
            abortRef.current?.abort();
        };
    }, [queryClient]);
}
