import type { DownloadTicket } from "../types/api";

interface PreparedTicketDownload {
  path: string;
  ticket: DownloadTicket;
}

export function createTicketDownloadUrl(
  path: string,
  ticket: string,
): string {
  const url = new URL(path, window.location.origin);
  url.searchParams.set("ticket", ticket);
  return url.toString();
}

export async function openTicketDownload(
  path: string,
  createTicket: () => Promise<DownloadTicket>,
): Promise<void> {
  await openPreparedTicketDownload(async () => ({
    path,
    ticket: await createTicket(),
  }));
}

export async function openPreparedTicketDownload(
  prepare: () => Promise<PreparedTicketDownload>,
): Promise<void> {
  const downloadWindow = window.open("", "_blank");

  try {
    const prepared = await prepare();
    const url = createTicketDownloadUrl(
      prepared.path,
      prepared.ticket.ticket,
    );
    if (downloadWindow) {
      downloadWindow.location.replace(url);
      return;
    }

    window.location.assign(url);
  } catch (error) {
    downloadWindow?.close();
    throw error;
  }
}
