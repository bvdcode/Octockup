// Prefilled whitelist of ignored paths for common backup source providers

export const IGNORED_PATHS_PRESETS: Record<string, string[]> = {
  "Octockup.Server.Modules.SFTPBackupStorage": [
    "/tmp",
    "/run",
    "/var/tmp",
    "/var/lock",
    "",
    "/var/lib/docker/containers/*/*-json.log",
    "/var/lib/docker/buildkit",
    "/var/lib/docker/overlay2",
    "/var/lib/docker/swarm",
    "/var/lib/docker/image",
    "/var/lib/docker/network",
    "/var/lib/docker/builder",
    "/var/lib/docker/tmp",
    "",
    "/var/log",
    "/var/cache",
    "",
    "/var/lib/systemd",
    "/run/sshd",
    "",
    "/swapfile",
    "/swap.img",
    "/dev",
    "/proc",

  ],
};

export function getIgnoredPathsPreset(backupModuleId: string): string[] {
  return IGNORED_PATHS_PRESETS[backupModuleId] ?? [];
}
