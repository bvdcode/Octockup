import { describe, expect, it } from "vitest";
import {
  SignalRConnectionManager,
  type SignalRConnectionCallbacks,
  type SignalRConnectionLifecycle,
  type SignalRRetryScheduler,
  type SignalRStartError,
} from "./SignalRConnectionManager";

describe("SignalRConnectionManager", () => {
  it("refreshes authorization after a 401 and retries once", async () => {
    const connection = new FakeConnection();
    connection.startBehaviors.push(
      () => Promise.reject<never>({ statusCode: 401 } satisfies SignalRStartError),
      () => Promise.resolve(),
    );
    const scheduler = new TestScheduler();
    const callbacks = new RecordingCallbacks();
    const manager = new SignalRConnectionManager(
      connection,
      callbacks,
      scheduler,
    );

    manager.start();
    await flushPromises();

    expect(callbacks.authorizationRefreshes).toBe(1);
    expect(scheduler.pendingDelays).toEqual([1000]);
    scheduler.runNext();
    await flushPromises();

    expect(connection.startCount).toBe(2);
    expect(callbacks.connectedCount).toBe(1);
    await manager.dispose();
  });

  it("tracks automatic reconnect and restarts after a hard close", async () => {
    const connection = new FakeConnection();
    const scheduler = new TestScheduler();
    const callbacks = new RecordingCallbacks();
    const manager = new SignalRConnectionManager(
      connection,
      callbacks,
      scheduler,
    );

    manager.start();
    await flushPromises();
    connection.triggerReconnecting();
    connection.triggerReconnected();
    connection.triggerClose();
    await flushPromises();

    expect(connection.startCount).toBe(2);
    expect(callbacks.connectedCount).toBe(3);
    expect(callbacks.disconnectedCount).toBe(2);
    await manager.dispose();
  });

  it("clears pending retries and suppresses callbacks after disposal", async () => {
    const connection = new FakeConnection();
    connection.startBehaviors.push(() =>
      Promise.reject<never>({ message: "offline" } satisfies SignalRStartError),
    );
    const scheduler = new TestScheduler();
    const callbacks = new RecordingCallbacks();
    const manager = new SignalRConnectionManager(
      connection,
      callbacks,
      scheduler,
      [250],
    );

    manager.start();
    await flushPromises();
    expect(scheduler.pendingDelays).toEqual([250]);

    await manager.dispose();
    connection.triggerReconnected();
    scheduler.runNext();

    expect(connection.stopCount).toBe(1);
    expect(connection.startCount).toBe(1);
    expect(callbacks.connectedCount).toBe(0);
    expect(scheduler.pendingDelays).toEqual([]);
  });
});

class FakeConnection implements SignalRConnectionLifecycle {
  public readonly startBehaviors: Array<() => Promise<void>> = [];
  public startCount = 0;
  public stopCount = 0;
  private closeCallback: () => void = () => undefined;
  private reconnectingCallback: () => void = () => undefined;
  private reconnectedCallback: () => void = () => undefined;

  public start(): Promise<void> {
    this.startCount += 1;
    return this.startBehaviors.shift()?.() ?? Promise.resolve();
  }

  public stop(): Promise<void> {
    this.stopCount += 1;
    return Promise.resolve();
  }

  public onclose(callback: () => void): void {
    this.closeCallback = callback;
  }

  public onreconnecting(callback: () => void): void {
    this.reconnectingCallback = callback;
  }

  public onreconnected(callback: () => void): void {
    this.reconnectedCallback = callback;
  }

  public triggerClose(): void {
    this.closeCallback();
  }

  public triggerReconnecting(): void {
    this.reconnectingCallback();
  }

  public triggerReconnected(): void {
    this.reconnectedCallback();
  }
}

class RecordingCallbacks implements SignalRConnectionCallbacks {
  public connectedCount = 0;
  public disconnectedCount = 0;
  public authorizationRefreshes = 0;

  public onConnected(): void {
    this.connectedCount += 1;
  }

  public onDisconnected(): void {
    this.disconnectedCount += 1;
  }

  public refreshAuthorization(): Promise<void> {
    this.authorizationRefreshes += 1;
    return Promise.resolve();
  }
}

class TestScheduler implements SignalRRetryScheduler {
  private readonly timers = new Map<number, { callback: () => void; delay: number }>();
  private nextTimerId = 1;

  public get pendingDelays(): number[] {
    return [...this.timers.values()].map((timer) => timer.delay);
  }

  public setTimeout(callback: () => void, delayMs: number): number {
    const timerId = this.nextTimerId;
    this.nextTimerId += 1;
    this.timers.set(timerId, { callback, delay: delayMs });
    return timerId;
  }

  public clearTimeout(timerId: number): void {
    this.timers.delete(timerId);
  }

  public runNext(): void {
    const next = this.timers.entries().next().value;
    if (!next) {
      return;
    }

    const [timerId, timer] = next;
    this.timers.delete(timerId);
    timer.callback();
  }
}

async function flushPromises(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
}
