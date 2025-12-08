import { useModulesApi } from "../api/modulesApi";
import BackupModuleWizard from "../components/BackupModuleWizard";

export default function SourceWizard() {
  const api = useModulesApi();

  return (
    <BackupModuleWizard moduleType="source" apiClient={api} backRoute="/sources" />
  );
}
