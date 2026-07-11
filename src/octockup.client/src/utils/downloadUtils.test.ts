import { describe, expect, it } from "vitest";
import {
  createTicketDownloadUrl,
  openPreparedTicketDownload,
  type TicketDownloadEnvironment,
  type TicketDownloadWindow,
} from "./downloadUtils";

describe("downloadUtils", () => {
  it("creates a scoped URL and navigates a pre-opened window", async () => {
    const environment = new RecordingDownloadEnvironment(true);

    await openPreparedTicketDownload(
      () =>
        Promise.resolve({
          path: "/api/v1/snapshots/job/download",
          ticket: { ticket: "one time+/ticket", expiresAt: "2030-01-01" },
        }),
      environment,
    );

    expect(environment.openCount).toBe(1);
    expect(environment.popup.replacedUrls).toEqual([
      "https://octockup.test/api/v1/snapshots/job/download?ticket=one+time%2B%2Fticket",
    ]);
    expect(environment.assignedUrls).toEqual([]);
  });

  it("falls back to current-window navigation when popups are blocked", async () => {
    const environment = new RecordingDownloadEnvironment(false);

    await openPreparedTicketDownload(
      () =>
        Promise.resolve({
          path: "/api/v1/backups/server",
          ticket: { ticket: "ticket", expiresAt: "2030-01-01" },
        }),
      environment,
    );

    expect(environment.assignedUrls).toEqual([
      "https://octockup.test/api/v1/backups/server?ticket=ticket",
    ]);
  });

  it("closes the placeholder window when ticket preparation fails", async () => {
    const environment = new RecordingDownloadEnvironment(true);
    const failure = new Error("ticket rejected");

    await expect(
      openPreparedTicketDownload(() => Promise.reject(failure), environment),
    ).rejects.toBe(failure);

    expect(environment.popup.closeCount).toBe(1);
    expect(environment.popup.replacedUrls).toEqual([]);
    expect(environment.assignedUrls).toEqual([]);
  });

  it("does not copy unrelated query parameters into a ticket URL", () => {
    const url = createTicketDownloadUrl(
      "/api/v1/file?format=raw",
      "download-ticket",
      "https://octockup.test",
    );

    expect(url).toBe(
      "https://octockup.test/api/v1/file?format=raw&ticket=download-ticket",
    );
    expect(url).not.toContain("access_token");
  });
});

class RecordingDownloadWindow implements TicketDownloadWindow {
  public readonly replacedUrls: string[] = [];
  public closeCount = 0;

  public replace(url: string): void {
    this.replacedUrls.push(url);
  }

  public close(): void {
    this.closeCount += 1;
  }
}

class RecordingDownloadEnvironment implements TicketDownloadEnvironment {
  public readonly origin = "https://octockup.test";
  public readonly popup = new RecordingDownloadWindow();
  public readonly assignedUrls: string[] = [];
  public openCount = 0;

  public constructor(private readonly popupAvailable: boolean) {}

  public openWindow(): TicketDownloadWindow | null {
    this.openCount += 1;
    return this.popupAvailable ? this.popup : null;
  }

  public assign(url: string): void {
    this.assignedUrls.push(url);
  }
}
