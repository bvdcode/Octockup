// Prefilled whitelist of ignored paths for common backup source providers

export const IGNORED_PATHS_PRESETS: Record<string, string[]> = {
  "Octockup.Server.Modules.SFTPBackupStorage": [
    "/tmp",
    "/var/tmp",
    "/run",
    "/run/lock",
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
    "/var/cache/apt",
    "/var/cache/yum",
    "/var/cache/dnf",
    "/var/cache/apk",
    "/var/cache/pacman/pkg",
    "",
    "/var/lib/systemd",
    "/run/sshd",
  ],
};

export function getIgnoredPathsPreset(backupModuleId: string): string[] {
  return IGNORED_PATHS_PRESETS[backupModuleId] ?? [];
}
