import { useBackupStoragesApi } from "../api/backupStoragesApi";
import BackupModuleWizard from "../components/BackupModuleWizard";

export default function StorageWizard() {
  const api = useBackupStoragesApi();

  return (
    <BackupModuleWizard moduleType="storage" apiClient={api} backRoute="/storages" />
  );
}
