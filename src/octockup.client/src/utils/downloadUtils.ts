import type { DownloadTicket } from "../types/api";

interface PreparedTicketDownload {
  path: string;
  ticket: DownloadTicket;
}

export interface TicketDownloadWindow {
  replace: (url: string) => void;
  close: () => void;
}

export interface TicketDownloadEnvironment {
  origin: string;
  openWindow: () => TicketDownloadWindow | null;
  assign: (url: string) => void;
}

export function createTicketDownloadUrl(
  path: string,
  ticket: string,
  origin = window.location.origin,
): string {
  const url = new URL(path, origin);
  url.searchParams.set("ticket", ticket);
  return url.toString();
}

export async function openTicketDownload(
  path: string,
  createTicket: () => Promise<DownloadTicket>,
  environment?: TicketDownloadEnvironment,
): Promise<void> {
  await openPreparedTicketDownload(async () => ({
    path,
    ticket: await createTicket(),
  }), environment);
}

export async function openPreparedTicketDownload(
  prepare: () => Promise<PreparedTicketDownload>,
  environment = createBrowserDownloadEnvironment(),
): Promise<void> {
  const downloadWindow = environment.openWindow();

  try {
    const prepared = await prepare();
    const url = createTicketDownloadUrl(
      prepared.path,
      prepared.ticket.ticket,
      environment.origin,
    );
    if (downloadWindow) {
      downloadWindow.replace(url);
      return;
    }

    environment.assign(url);
  } catch (error) {
    downloadWindow?.close();
    throw error;
  }
}

function createBrowserDownloadEnvironment(): TicketDownloadEnvironment {
  return {
    origin: window.location.origin,
    openWindow: () => {
      const downloadWindow = window.open("", "_blank");
      if (!downloadWindow) {
        return null;
      }

      return {
        replace: (url) => downloadWindow.location.replace(url),
        close: () => downloadWindow.close(),
      };
    },
    assign: (url) => window.location.assign(url),
  };
}
