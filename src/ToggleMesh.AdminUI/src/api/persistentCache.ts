import { dehydrate, hydrate, QueryClient } from '@tanstack/react-query';
import { get, set, del } from 'idb-keyval';

const CACHE_KEY = 'REACT_QUERY_OFFLINE_CACHE';
const MAX_QUERIES = 150;

export async function hydrateCache(queryClient: QueryClient) {
    try {
        const savedState = await get(CACHE_KEY);
        if (savedState) {
            hydrate(queryClient, savedState);
            console.log('[Cache] Successfully hydrated from IndexedDB.');
        }
    } catch (e) {
        console.error('Failed to hydrate React Query cache from IndexedDB:', e);
    }
}

export function persistCache(queryClient: QueryClient) {
    let saveCacheTimeout: ReturnType<typeof setTimeout>;

    return queryClient.getQueryCache().subscribe((event) => {
        if (event.type === 'updated' && event.action.type === 'success') {
            clearTimeout(saveCacheTimeout);

            saveCacheTimeout = setTimeout(async () => {
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

                    if (state.queries.length > MAX_QUERIES) {
                        console.warn(`[Cache] Cache has ${state.queries.length} queries, pruning to ${MAX_QUERIES}...`);
                        const sortedQueries = [...state.queries].sort((a, b) =>
                            (b.state.dataUpdatedAt || 0) - (a.state.dataUpdatedAt || 0)
                        );
                        state.queries = sortedQueries.slice(0, MAX_QUERIES);
                    }

                    await set(CACHE_KEY, state);
                } catch (e) {
                    console.error('Failed to persist React Query cache to IndexedDB:', e);
                    if (e instanceof Error && e.name === 'QuotaExceededError') {
                        await del(CACHE_KEY).catch(() => {});
                    }
                }
            }, 1000);
        }
    });
}