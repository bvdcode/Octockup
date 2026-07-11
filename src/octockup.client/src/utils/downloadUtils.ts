import type { DownloadTicket } from "../types/api";

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
  const downloadWindow = window.open("", "_blank");

  try {
    const ticket = await createTicket();
    const url = createTicketDownloadUrl(path, ticket.ticket);
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
