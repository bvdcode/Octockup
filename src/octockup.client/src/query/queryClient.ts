import { QueryClient } from "@tanstack/react-query";

const CACHE_DURATION_MS = 30 * 60 * 1_000;
const STALE_DURATION_MS = 30 * 1_000;

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      gcTime: CACHE_DURATION_MS,
      staleTime: STALE_DURATION_MS,
      refetchOnWindowFocus: false,
      retry: 2,
    },
  },
});
