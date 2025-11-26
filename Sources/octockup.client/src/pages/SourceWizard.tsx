import { useTranslation } from "react-i18next";
import { useBackupSourcesApi } from "../api/backupSourcesApi";
import BackupModuleWizard from "../components/BackupModuleWizard";

export default function SourceWizard() {
  const { t } = useTranslation();
  const api = useBackupSourcesApi();

  return (
    <BackupModuleWizard
      moduleType="source"
      apiClient={api}
      backRoute="/sources"
      translations={{
        title: t("sources.newSource"),
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
        create: t("sources.create"),
        back: t("common.back"),
        creating: t("wizard.creating"),
        unsavedChanges: t("wizard.unsavedChanges"),
        createSuccess: t("wizard.sourceCreatedSuccess"),
        createError: t("wizard.createError"),
      }}
    />
  );
}
