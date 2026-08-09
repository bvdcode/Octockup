import AuthenticationSettingsCard from "./AuthenticationSettingsCard";
import OidcProvidersCard from "./OidcProvidersCard";
import StorageCleanupCard from "./StorageCleanupCard";
import UserManagementCard from "./UserManagementCard";

interface AdminSettingsSectionProps {
  isAdmin: boolean;
  onProvidersChanged: () => void;
}

export default function AdminSettingsSection({
  isAdmin,
  onProvidersChanged,
}: AdminSettingsSectionProps) {
  if (!isAdmin) {
    return null;
  }

  return (
    <>
      <AuthenticationSettingsCard />
      <OidcProvidersCard onProvidersChanged={onProvidersChanged} />
      <UserManagementCard />
      <StorageCleanupCard />
    </>
  );
}
