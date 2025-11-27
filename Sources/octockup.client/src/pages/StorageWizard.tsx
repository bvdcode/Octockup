import { useModulesApi } from "../api/modulesApi";
import BackupModuleWizard from "../components/BackupModuleWizard";

export default function StorageWizard() {
  const api = useModulesApi();

  return (
    <BackupModuleWizard moduleType="target" apiClient={api} backRoute="/storages" />
  );
}
