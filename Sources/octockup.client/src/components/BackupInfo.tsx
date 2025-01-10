import React from "react";
import { Button } from "@mui/material";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";

const BackupInfo: React.FC = () => {
  const { id } = useParams<{ id: string }>();

  const { t } = useTranslation();
  // Logic for data fetching and table rendering will be added here

  return (
    <div>
      <Button
        onClick={() => {
          window.history.back();
        }}
      >
        {t("backupInfo.back")}
      </Button>
      <h1>Backup Information</h1>
      <p>Backup ID: {id}</p>
      {/* Table rendering will be added here */}
    </div>
  );
};

export default BackupInfo;
