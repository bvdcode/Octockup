// Prefilled whitelist of ignored paths for common backup source providers

export const IGNORED_PATHS_PRESETS: Record<string, string[]> = {
  "Octockup.Server.Modules.SFTPBackupStorage": [
    "/tmp",
    "/run",
    "/dev",
    "/proc",
    "/sys",
    "/var/lib",
    "/var/tmp",
    "/var/lock",
    "/var/log",
    "/var/cache",
    "/etc/apt",
    "",
    "/swapfile",
    "/swap.img",
    "/lost+found",
    "",
    "/usr/src",
    "/usr/lib",
    "/usr/bin",
    "/usr/sbin",
    "/usr/share",
    "/usr/include",
  ],
};

export function getIgnoredPathsPreset(backupModuleId: string): string[] {
  return IGNORED_PATHS_PRESETS[backupModuleId] ?? [];
}
