import { useBackupSourcesApi } from "../api/backupSourcesApi";
import BackupModuleWizard from "../components/BackupModuleWizard";

export default function SourceWizard() {
  const api = useBackupSourcesApi();

  return (
    <BackupModuleWizard moduleType="source" apiClient={api} backRoute="/sources" />
  );
}
