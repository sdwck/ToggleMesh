import { dehydrate, hydrate, QueryClient } from '@tanstack/react-query';

const CACHE_KEY = 'REACT_QUERY_OFFLINE_CACHE';
const MAX_CACHE_SIZE_BYTES = 2.5 * 1024 * 1024;

export function hydrateCache(queryClient: QueryClient) {
    try {
        const savedState = localStorage.getItem(CACHE_KEY);
        if (savedState) {
            hydrate(queryClient, JSON.parse(savedState));
            console.log('[Cache] Successfully hydrated from localStorage.');
        }
    } catch (e) {
        console.error('Failed to hydrate React Query cache:', e);
    }
}

export function persistCache(queryClient: QueryClient) {
    let saveCacheTimeout: ReturnType<typeof setTimeout>;

    return queryClient.getQueryCache().subscribe((event) => {
        if (event.type === 'updated' && event.action.type === 'success') {
            clearTimeout(saveCacheTimeout);

            saveCacheTimeout = setTimeout(() => {
                try {
                    let state = dehydrate(queryClient, {
                        shouldDehydrateQuery(query) {
                            if (query.state.status !== 'success') return false;

                            const queryKeyStr = JSON.stringify(query.queryKey);
                            if (queryKeyStr.includes('audit')) return false;
                            if (queryKeyStr.includes('metrics')) return false;

                            return true;
                        }
                    });


                    state.queries = state.queries.map((q) => {
                        const data = q.state.data as any;
                        if (data && Array.isArray(data.pages) && Array.isArray(data.pageParams)) {
                            return {
                                ...q,
                                state: {
                                    ...q.state,
                                    data: {
                                        ...data,
                                        pages: data.pages.slice(0, 1),
                                        pageParams: data.pageParams.slice(0, 1)
                                    }
                                }
                            };
                        }
                        return q;
                    });

                    let serializedState = JSON.stringify(state);

                    if (serializedState.length > MAX_CACHE_SIZE_BYTES) {
                        console.warn(
                            `[Cache] Cache size (${(serializedState.length / 1024 / 1024).toFixed(2)}MB) exceeds limit of 2.5MB. Pruning oldest queries...`
                        );

                        if (serializedState.length > MAX_CACHE_SIZE_BYTES) {
                            const sortedQueries = [...state.queries].sort((a, b) =>
                                (b.state.dataUpdatedAt || 0) - (a.state.dataUpdatedAt || 0)
                            );

                            const percentToKeep = 0.7;
                            const itemsToKeep = Math.floor(sortedQueries.length * percentToKeep);
                            state.queries = sortedQueries.slice(0, itemsToKeep);

                            serializedState = JSON.stringify(state);
                        }

                        console.log(
                            `[Cache] Pruned state successfully. New size: ${(serializedState.length / 1024 / 1024).toFixed(2)}MB`
                        );
                    }

                    localStorage.setItem(CACHE_KEY, serializedState);
                } catch (e) {
                    console.error('Failed to persist React Query cache:', e);

                    if (e instanceof Error && e.name === 'QuotaExceededError') {
                        localStorage.removeItem(CACHE_KEY);
                    }
                }
            }, 1000);
        }
    });
}