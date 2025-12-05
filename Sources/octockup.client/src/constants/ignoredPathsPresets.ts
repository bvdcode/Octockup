// Prefilled whitelist of ignored paths for common backup source providers

export const IGNORED_PATHS_PRESETS: Record<string, string[]> = {
  "Octockup.Server.Modules.SFTPBackupStorage": [
    "/tmp",
    "/run",
    "/dev",
    "/proc",
    "/sys",
    "/var/tmp",
    "/var/lock",
    "/var/log",
    "/var/cache",
    "",
    "/swapfile",
    "/swap.img",
    "",
    "/var/lib/systemd",
    "/var/lib/docker/tmp",
    "/var/lib/docker/swarm",
    "/var/lib/docker/image",
    "/var/lib/docker/network",
    "/var/lib/docker/builder",
    "/var/lib/docker/buildkit",
    "/var/lib/docker/overlay2",
    "/var/lib/docker/containers/*/*-json.log",
  ],
};

export function getIgnoredPathsPreset(backupModuleId: string): string[] {
  return IGNORED_PATHS_PRESETS[backupModuleId] ?? [];
}
