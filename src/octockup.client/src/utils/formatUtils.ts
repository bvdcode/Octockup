export function formatSpeed(bytesPerSecond: number): string {
  const mbPerSecond = bytesPerSecond / (1024 * 1024);
  if (mbPerSecond < 0.01) {
    const kbPerSecond = bytesPerSecond / 1024;
    return `${kbPerSecond.toFixed(2)} KB/s`;
  }
  return `${mbPerSecond.toFixed(2)} MB/s`;
}

export function formatElapsed(elapsed?: string): string {
  if (!elapsed) {
    return "";
  }
  const parsed = parseElapsedParts(elapsed);
  if (parsed === null) {
    return elapsed;
  }
  const { hours, minutes, seconds } = parsed;

  if (hours > 0) {
    return `${hours}h ${minutes}m ${seconds}s`;
  }
  if (minutes > 0) {
    return `${minutes}m ${seconds}s`;
  }
  return `${seconds}s`;
}

export function parseElapsedToSeconds(elapsed?: string): number | null {
  if (!elapsed) {
    return null;
  }
  const parsed = parseElapsedParts(elapsed);
  return parsed === null
    ? null
    : parsed.hours * 3600 + parsed.minutes * 60 + parsed.seconds;
}

interface ElapsedParts {
  hours: number;
  minutes: number;
  seconds: number;
}

function parseElapsedParts(elapsed: string): ElapsedParts | null {
  const parts = elapsed.split(":");
  if (parts.length < 3) {
    return null;
  }

  let hours: number;
  if (parts[0].includes(".")) {
    const dayHour = parts[0].split(".");
    const days = Number.parseInt(dayHour[0]);
    const hh = Number.parseInt(dayHour[1]);
    if (Number.isNaN(days) || Number.isNaN(hh)) {
      return null;
    }
    hours = hh + days * 24;
  } else {
    const hh = Number.parseInt(parts[0]);
    if (Number.isNaN(hh)) {
      return null;
    }
    hours = hh;
  }

  const minutes = Number.parseInt(parts[1]);
  const seconds = Math.floor(Number.parseFloat(parts[2]));
  if (Number.isNaN(minutes) || Number.isNaN(seconds)) {
    return null;
  }
  return { hours, minutes, seconds };
}

export function formatDurationShort(totalSeconds: number): string {
  if (!Number.isFinite(totalSeconds) || totalSeconds < 0) return "";
  const seconds = Math.round(totalSeconds);
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;

  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}

export function formatSize(bytes: number): string {
  if (bytes === 0) return "0 B";

  const k = 1024;
  const sizes = ["B", "KB", "MB", "GB", "TB"];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return `${(bytes / Math.pow(k, i)).toFixed(2)} ${sizes[i]}`;
}
