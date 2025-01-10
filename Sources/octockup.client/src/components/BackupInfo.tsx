import {
  Box,
  Button,
  Pagination,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
} from "@mui/material";
import React, { useEffect } from "react";
import { useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { deleteSnapshot, getSnapshots } from "../api/api";
import { BackupSnapshot } from "../api/types";
import { ArrowBack, Delete, Refresh } from "@mui/icons-material";
import CustomDialog from "./CustomDialog";
import { toast } from "react-toastify";

const BackupInfo: React.FC = () => {
  const [page, setPage] = React.useState(1);
  const { id } = useParams<{ id: string }>();
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

  const handleSnapshotDelete = (snapshotId: number) => {
    deleteSnapshot(snapshotId).then(() => {
      toast.success(t("backupInfo.deleteSuccess"));
      getSnapshots(parseInt(id || ""), page, pageSize).then((response) => {
        setData(response.data);
        setTotalCount(response.totalCount);
      });
    });
  };

  return (
    <Box
      sx={{
        padding: 2,
        marginBottom: 2,
        display: "flex",
        flexDirection: "column",
        gap: 2,
        width: "100%",
      }}
    >
      <Box display="flex" justifyContent="space-between">
        <Button
          onClick={() => {
            window.history.back();
          }}
          sx={{ minWidth: "unset", alignSelf: "flex-start" }}
        >
          <ArrowBack />
        </Button>
        <Button
          onClick={() => {
            setData([]);
            getSnapshots(parseInt(id || ""), page, pageSize).then(
              (response) => {
                setData(response.data);
                setTotalCount(response.totalCount);
              }
            );
          }}
          sx={{ minWidth: "unset", alignSelf: "flex-start" }}
        >
          <Refresh />
        </Button>
      </Box>
      <Paper
        sx={{
          padding: 2,
          marginBottom: 2,
          display: "flex",
          justifyContent: "space-between",
          gap: 2,
        }}
      >
        <Box>{t("backupInfo.title")}</Box>
        <Box># {id}</Box>
      </Paper>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>{t("backupInfo.id")}</TableCell>
              <TableCell>{t("backupInfo.createdAt")}</TableCell>
              <TableCell>{t("backupInfo.elapsed")}</TableCell>
              <TableCell>{t("backupInfo.fileCount")}</TableCell>
              <TableCell>{t("backupInfo.totalSize")}</TableCell>
              <TableCell>{t("backupInfo.actions")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.map((row) => (
              <TableRow key={row.id}>
                <TableCell>{row.id}</TableCell>
                <TableCell>{row.createdAtDate.toLocaleString()}</TableCell>
                <TableCell>{row.elapsed}</TableCell>
                <TableCell>{row.fileCount}</TableCell>
                <TableCell>{row.totalSizeFormatted}</TableCell>
                <TableCell>
                  <CustomDialog
                    title={t("backupInfo.deleteTitle")}
                    content={t("backupInfo.deleteMessage", {
                      id: row.id,
                    })}
                    cancelText={t("cancel")}
                    confirmText={t("confirm")}
                    doubleCheck={true}
                    onConfirm={() => handleSnapshotDelete(row.id)}
                  >
                    <Button sx={{ minWidth: "unset" }}>
                      <Delete sx={{ cursor: "pointer" }} />
                    </Button>
                  </CustomDialog>
                </TableCell>
              </TableRow>
            ))}
            {data.length === 0 && (
              <TableRow>
                <TableCell sx={{ textAlign: "center" }} colSpan={3}>
                  {t("backupInfo.noData")}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

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
    </Box>
  );
};

export default BackupInfo;
