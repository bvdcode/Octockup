export class LatestValueByKeyThrottler<TKey, TValue> {
  private readonly pending = new Map<TKey, TValue>();
  private timer: ReturnType<typeof setTimeout> | null = null;

  public constructor(
    private readonly apply: (value: TValue) => void,
    private readonly intervalMs: number,
  ) {
    if (intervalMs <= 0) {
      throw new RangeError("Throttle interval must be positive.");
    }
  }

  public push(key: TKey, value: TValue, immediate = false): void {
    if (immediate) {
      this.pending.delete(key);
      this.apply(value);
      if (this.pending.size === 0) {
        this.clearTimer();
      }
      return;
    }

    this.pending.set(key, value);
    if (this.timer === null) {
      this.timer = setTimeout(() => this.flush(), this.intervalMs);
    }
  }

  public dispose(): void {
    this.clearTimer();
    this.pending.clear();
  }

  private flush(): void {
    const values = [...this.pending.values()];
    this.pending.clear();
    this.timer = null;
    values.forEach((value) => this.apply(value));
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
