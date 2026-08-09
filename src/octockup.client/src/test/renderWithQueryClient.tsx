import {
  QueryClient,
  QueryClientProvider,
} from "@tanstack/react-query";
import {
  render,
  type RenderResult,
} from "@testing-library/react";
import type { ReactElement } from "react";

export function createTestQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: Number.POSITIVE_INFINITY,
      },
    },
  });
}

export function renderWithQueryClient(
  element: ReactElement,
  queryClient: QueryClient = createTestQueryClient(),
): RenderResult {
  return render(
    <QueryClientProvider client={queryClient}>
      {element}
    </QueryClientProvider>,
  );
}
