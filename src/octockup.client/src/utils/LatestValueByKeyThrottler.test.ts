import { afterEach, describe, expect, it, vi } from "vitest";
import { LatestValueByKeyThrottler } from "./LatestValueByKeyThrottler";

describe("LatestValueByKeyThrottler", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("coalesces repeated updates and preserves independent keys", () => {
    vi.useFakeTimers();
    const applied: number[] = [];
    const throttler = new LatestValueByKeyThrottler<string, number>(
      (value) => applied.push(value),
      250,
    );

    for (let value = 1; value <= 100; value += 1) {
      throttler.push("first", value);
    }
    throttler.push("second", 200);

    vi.advanceTimersByTime(249);
    expect(applied).toEqual([]);
    vi.advanceTimersByTime(1);
    expect(applied).toEqual([100, 200]);
  });

  it("applies terminal values immediately without replaying stale state", () => {
    vi.useFakeTimers();
    const applied: number[] = [];
    const throttler = new LatestValueByKeyThrottler<string, number>(
      (value) => applied.push(value),
      250,
    );

    throttler.push("job", 1);
    throttler.push("job", 2, true);
    expect(applied).toEqual([2]);

    vi.advanceTimersByTime(250);
    expect(applied).toEqual([2]);
  });

  it("drops pending values when disposed", () => {
    vi.useFakeTimers();
    const applied: number[] = [];
    const throttler = new LatestValueByKeyThrottler<string, number>(
      (value) => applied.push(value),
      250,
    );

    throttler.push("job", 1);
    throttler.dispose();
    vi.runAllTimers();

    expect(applied).toEqual([]);
  });
});
