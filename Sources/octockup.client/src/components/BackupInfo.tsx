import { Box, Button, Pagination, TextField } from "@mui/material";
import React, { useEffect } from "react";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { getSnapshots } from "../api/api";
import { BackupSnapshot } from "../api/types";

const BackupInfo: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(10);
  const [totalCount, setTotalCount] = React.useState(0);
  const [data, setData] = React.useState<BackupSnapshot[]>([]);

  const { t } = useTranslation();

  useEffect(() => {
    const parsedId = parseInt(id || "");
    getSnapshots(parsedId, page, pageSize).then((response) => {
      setData(response.data);
      setTotalCount(response.totalCount);
    });
  }, [id, page, pageSize]);

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

      <Box
        display="flex"
        justifyContent="center"
        alignItems="center"
        mt={2}
        sx={{ marginBottom: { xs: 5 } }}
      >
        <Pagination
          count={Math.ceil(totalCount / pageSize)}
          color="primary"
          shape="rounded"
          size="medium"
          showFirstButton
          showLastButton
          boundaryCount={0}
          page={page}
          onChange={(_, page) => {
            setPage(page);
          }}
        />
        <Box ml={2} sx={{ display: { xs: "none", sm: "block" } }}>
          <TextField
            select
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value))}
            variant="outlined"
            size="small"
            sx={{ position: "absolute", right: 15, marginTop: "-20px" }}
            slotProps={{
              select: {
                native: true,
              },
            }}
          >
            {[1, 5, 10, 25, 50, 100].map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </TextField>
        </Box>
      </Box>
    </div>
  );
};

export default BackupInfo;
