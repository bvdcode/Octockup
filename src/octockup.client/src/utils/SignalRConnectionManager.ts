export interface SignalRConnectionLifecycle {
  start: () => Promise<void>;
  stop: () => Promise<void>;
  onclose: (callback: () => void) => void;
  onreconnecting: (callback: () => void) => void;
  onreconnected: (callback: () => void) => void;
}

export interface SignalRConnectionCallbacks {
  onConnected: () => void;
  onDisconnected: () => void;
  refreshAuthorization: () => Promise<void>;
}

export interface SignalRRetryScheduler {
  setTimeout: (callback: () => void, delayMs: number) => number;
  clearTimeout: (timerId: number) => void;
}

export interface SignalRStartError {
  statusCode?: number;
  message?: string;
}

const defaultRetryDelaysMs = [0, 2000, 5000, 10000, 30000];
const authorizationRetryDelayMs = 1000;

export function isSignalRUnauthorized(error: SignalRStartError): boolean {
  return error.statusCode === 401 || error.message?.includes("401") === true;
}

export class SignalRConnectionManager {
  private retryIndex = 0;
  private retryTimerId: number | null = null;
  private starting = false;
  private refreshingAuthorization = false;
  private disposed = false;

  public constructor(
    private readonly connection: SignalRConnectionLifecycle,
    private readonly callbacks: SignalRConnectionCallbacks,
    private readonly scheduler: SignalRRetryScheduler,
    private readonly retryDelaysMs: readonly number[] = defaultRetryDelaysMs,
  ) {
    if (retryDelaysMs.length === 0) {
      throw new RangeError("At least one retry delay is required.");
    }

    connection.onclose(() => this.handleClose());
    connection.onreconnecting(() => this.handleReconnecting());
    connection.onreconnected(() => this.handleReconnected());
  }

  public start(): void {
    this.startWithRetry();
  }

  public async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.clearRetryTimer();
    this.callbacks.onDisconnected();
    await this.connection.stop().catch(() => undefined);
  }

  private startWithRetry(): void {
    if (this.disposed || this.starting) {
      return;
    }

    this.starting = true;
    void this.connection
      .start()
      .then(() => {
        this.starting = false;
        if (this.disposed) {
          return;
        }

        this.retryIndex = 0;
        this.clearRetryTimer();
        this.callbacks.onConnected();
      })
      .catch((error: SignalRStartError) => {
        this.starting = false;
        if (this.disposed) {
          return;
        }

        this.callbacks.onDisconnected();
        if (isSignalRUnauthorized(error)) {
          void this.refreshAuthorizationAndRetry();
          return;
        }

        const index = Math.min(
          this.retryIndex,
          this.retryDelaysMs.length - 1,
        );
        const delayMs = this.retryDelaysMs[index];
        this.retryIndex += 1;
        this.scheduleRetry(delayMs);
      });
  }

  private async refreshAuthorizationAndRetry(): Promise<void> {
    if (this.refreshingAuthorization || this.disposed) {
      return;
    }

    this.refreshingAuthorization = true;
    await this.callbacks.refreshAuthorization().catch(() => undefined);
    this.refreshingAuthorization = false;
    if (!this.disposed) {
      this.scheduleRetry(authorizationRetryDelayMs);
    }
  }

  private scheduleRetry(delayMs: number): void {
    this.clearRetryTimer();
    this.retryTimerId = this.scheduler.setTimeout(() => {
      this.retryTimerId = null;
      this.startWithRetry();
    }, delayMs);
  }

  private clearRetryTimer(): void {
    if (this.retryTimerId === null) {
      return;
    }

    this.scheduler.clearTimeout(this.retryTimerId);
    this.retryTimerId = null;
  }

  private handleClose(): void {
    if (this.disposed) {
      return;
    }

    this.callbacks.onDisconnected();
    this.startWithRetry();
  }

  private handleReconnecting(): void {
    if (!this.disposed) {
      this.callbacks.onDisconnected();
    }
  }

  private handleReconnected(): void {
    if (this.disposed) {
      return;
    }

    this.retryIndex = 0;
    this.clearRetryTimer();
    this.callbacks.onConnected();
  }
}
