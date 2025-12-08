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
    "/lost+found",
    "",
    "/var/lib/apt",
    "/var/lib/systemd",
    "/var/lib/snapd/snaps",
    "/var/lib/docker/tmp",
    "/var/lib/docker/swarm",
    "/var/lib/docker/image",
    "/var/lib/docker/network",
    "/var/lib/docker/builder",
    "/var/lib/docker/buildkit",
    "/var/lib/docker/overlay2",
    "/var/lib/systemd/coredump",
    "/var/lib/docker/containers/*/*.log",
    "/var/lib/docker/containers/*/*.log.*",
    "/var/lib/docker/containers/*/*-json.log",
    "",
    "/usr/src",
    "/usr/bin",
    "/usr/sbin",
    "/usr/share",
    "/usr/include",
    "/usr/lib/python3/dist-packages",
  ],
};

export function getIgnoredPathsPreset(backupModuleId: string): string[] {
  return IGNORED_PATHS_PRESETS[backupModuleId] ?? [];
}
