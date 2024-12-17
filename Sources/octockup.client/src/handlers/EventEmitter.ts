type EventHandler = (...args: unknown[]) => void;

class EventEmitter {
  private events: { [key: string]: EventHandler[] } = {};

  on(event: string, handler: EventHandler) {
    if (!this.events[event]) {
      this.events[event] = [];
    }
    this.events[event].push(handler);
  }

  off(event: string, handler: EventHandler) {
    if (!this.events[event]) return;
    this.events[event] = this.events[event].filter((h) => h !== handler);
  }

  emit(event: string, ...args: unknown[]) {
    if (!this.events[event]) return;
    this.events[event].forEach((handler) => handler(...args));
  }
}

export default EventEmitter;
