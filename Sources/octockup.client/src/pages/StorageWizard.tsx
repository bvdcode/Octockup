import { useTranslation } from "react-i18next";
import { useBackupStoragesApi } from "../api/backupStoragesApi";
import BackupModuleWizard from "../components/BackupModuleWizard";

export default function StorageWizard() {
  const { t } = useTranslation();
  const api = useBackupStoragesApi();

  return (
    <BackupModuleWizard
      moduleType="storage"
      apiClient={api}
      backRoute="/storages"
      translations={{
        title: t("storages.newStorage"),
        tag: t("wizard.tag"),
        enterTag: t("wizard.enterTag"),
        testConnection: t("wizard.testConnection"),
        testing: t("wizard.testing"),
        testSuccess: t("wizard.testSuccess"),
        testFailed: t("wizard.testFailed"),
        fillParameters: t("wizard.fillParameters"),
        testResult: t("wizard.testResult"),
        fileName: t("wizard.fileName"),
        filePath: t("wizard.filePath"),
        fileSize: t("wizard.fileSize"),
        fileModified: t("wizard.fileModified"),
        directoryBrowser: t("wizard.directoryBrowser"),
        up: t("wizard.up"),
        noSubdirectories: t("wizard.noSubdirectories"),
        clickToLoad: t("wizard.clickToLoad"),
        loadRootDirectories: t("wizard.loadRootDirectories"),
        create: t("storages.create"),
        back: t("common.back"),
        creating: t("wizard.creating"),
        unsavedChanges: t("wizard.unsavedChanges"),
        createSuccess: t("wizard.storageCreatedSuccess"),
        createError: t("wizard.createError"),
      }}
    />
  );
}
