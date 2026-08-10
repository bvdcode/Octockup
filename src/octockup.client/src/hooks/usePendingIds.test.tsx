import { act, renderHook } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { usePendingIds } from "./usePendingIds";

interface DeferredRequest {
  promise: Promise<void>;
  resolve: () => void;
}

function createDeferredRequest(): DeferredRequest {
  let resolve: () => void = () => {};
  const promise = new Promise<void>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
}

describe("usePendingIds", () => {
  it("keeps concurrent item actions pending independently", async () => {
    const firstRequest = createDeferredRequest();
    const secondRequest = createDeferredRequest();
    const { result } = renderHook(() => usePendingIds());

    let firstAction: Promise<void> = Promise.resolve();
    let secondAction: Promise<void> = Promise.resolve();
    await act(async () => {
      firstAction = result.current.run("first", () => firstRequest.promise);
      secondAction = result.current.run("second", () => secondRequest.promise);
    });

    expect(result.current.has("first")).toBe(true);
    expect(result.current.has("second")).toBe(true);

    await act(async () => {
      firstRequest.resolve();
      await firstAction;
    });

    expect(result.current.has("first")).toBe(false);
    expect(result.current.has("second")).toBe(true);

    await act(async () => {
      secondRequest.resolve();
      await secondAction;
    });

    expect(result.current.has("second")).toBe(false);
  });
});
