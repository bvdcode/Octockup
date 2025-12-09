export function formatSpeed(bytesPerSecond: number): string {
  const mbPerSecond = bytesPerSecond / (1024 * 1024);
  if (mbPerSecond < 0.01) {
    const kbPerSecond = bytesPerSecond / 1024;
    return `${kbPerSecond.toFixed(2)} KB/s`;
  }
  return `${mbPerSecond.toFixed(2)} MB/s`;
}

export function formatElapsed(elapsed?: string): string {
  if (!elapsed) return "";
  
  const parts = elapsed.split(":");
  if (parts.length < 3) return elapsed;

  let hours = 0;
  let minutes = 0;
  let seconds = 0;

  if (parts[0].includes(".")) {
    // Format: DD.HH:MM:SS.mmmmmmm
    const dayHour = parts[0].split(".");
    const days = parseInt(dayHour[0]);
    hours = parseInt(dayHour[1]) + days * 24;
    minutes = parseInt(parts[1]);
    seconds = Math.floor(parseFloat(parts[2]));
  } else {
    // Format: HH:MM:SS.mmmmmmm
    hours = parseInt(parts[0]);
    minutes = parseInt(parts[1]);
    seconds = Math.floor(parseFloat(parts[2]));
  }

  if (hours > 0) {
    return `${hours}h ${minutes}m ${seconds}s`;
  } else if (minutes > 0) {
    return `${minutes}m ${seconds}s`;
  } else {
    return `${seconds}s`;
  }
}

export function formatSize(bytes: number): string {
  if (bytes === 0) return "0 B";
  
  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  
  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
}
