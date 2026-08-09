import {
  Box,
  Card,
  Stack,
  Alert,
  Divider,
  Tooltip,
  Snackbar,
  IconButton,
  Typography,
  CardContent,
  CircularProgress,
} from "@mui/material";
import { useState } from "react";
import { confirm } from "material-ui-confirm";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ModuleDestination } from "../types/api";
import { parseUtcDate } from "../utils/dateUtils";
import { DeleteOutline } from "@mui/icons-material";
import { AddCircleOutline } from "@mui/icons-material";
import { getSourceIcon } from "../constants/sourceIcons";
import { EditableModuleTag } from "../components/EditableModuleTag";
import { useModuleCatalog } from "../hooks/useModuleCatalog";
import { getApiErrorMessage } from "../utils/apiError";

export function SourcesPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const {
    modules: userSources,
    providers: availableSources,
    isPending,
    hasData,
    error,
    renameModule,
    deleteModule,
  } = useModuleCatalog(ModuleDestination.Source, "source");
  const [snackbar, setSnackbar] = useState<string | null>(null);

  const handleRename = async (moduleId: string, newTag: string) => {
    try {
      await renameModule(moduleId, newTag);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setSnackbar(
          getApiErrorMessage(caughtError, t("sources.renameFailed")),
        );
      }
      throw caughtError;
    }
  };

  if (isPending) {
    return (
      <Box display="flex" justifyContent="center" p={4}>
        <CircularProgress />
      </Box>
    );
  }

  if (error && !hasData) {
    return (
      <Box p={2}>
        <Alert severity="error">
          {getApiErrorMessage(error, t("sources.loadFailed"))}
        </Alert>
      </Box>
    );
  }

  return (
    <Stack spacing={3}>
      <Box>
        <Typography variant="h5" gutterBottom>
          {t("sources.title")}
        </Typography>
        {userSources.length === 0 ? (
          <Card>
            <CardContent>
              <Typography color="text.secondary">
                {t("sources.noSources")}
              </Typography>
            </CardContent>
          </Card>
        ) : (
          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: {
                xs: "repeat(auto-fill, minmax(140px, 1fr))",
                sm: "repeat(auto-fill, minmax(160px, 1fr))",
              },
              gap: 2,
            }}
          >
            {userSources.map((s) => (
              <Card
                key={s.tag}
                sx={{
                  height: 140,
                  display: "flex",
                  alignItems: "stretch",
                  justifyContent: "center",
                  position: "relative",
                }}
              >
                <Tooltip title={t("sources.deleteTooltip")} placement="left">
                  <IconButton
                    size="small"
                    aria-label={t("common.delete")}
                    sx={{
                      position: "absolute",
                      top: 4,
                      right: 4,
                    }}
                    onClick={async (e) => {
                      e.stopPropagation();
                      const result = await confirm({
                        title: t("sources.deleteTitle"),
                        description: t("sources.deleteText"),
                        confirmationText: t("common.delete"),
                        cancellationText: t("common.cancel"),
                        confirmationButtonProps: { color: "error" },
                      });
                      if (result.confirmed) {
                        await deleteModule(s.id);
                      }
                    }}
                  >
                    <DeleteOutline fontSize="small" color="primary" />
                  </IconButton>
                </Tooltip>
                <CardContent
                  sx={{
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    gap: 0.5,
                    justifyContent: "space-between",
                    height: "100%",
                    p: 2,
                  }}
                >
                  <Box sx={{ fontSize: 32 }}>
                    {getSourceIcon(s.backupModuleId)}
                  </Box>
                  <EditableModuleTag
                    tag={s.tag}
                    onRename={(newTag) => handleRename(s.id, newTag)}
                  />
                  <Typography
                    variant="caption"
                    sx={{
                      textAlign: "center",
                      maxWidth: 140,
                      color: "text.secondary",
                      fontSize: "0.7rem",
                    }}
                  >
                    {parseUtcDate(s.createdAt)!.toLocaleDateString()}
                  </Typography>
                </CardContent>
              </Card>
            ))}
          </Box>
        )}
      </Box>
      <Divider />
      <Box>
        <Typography variant="h6" gutterBottom>
          {t("sources.addNew")}
        </Typography>
        <Box
          sx={{
            display: "grid",
            gridTemplateColumns: {
              xs: "repeat(auto-fill, minmax(140px, 1fr))",
              sm: "repeat(auto-fill, minmax(160px, 1fr))",
            },
            gap: 2,
          }}
        >
          {availableSources.map((s) => (
            <Card
              key={s.id}
              sx={{
                height: 140,
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                "&:hover": { bgcolor: "action.hover" },
              }}
              onClick={() => {
                navigate(`/sources/new?type=${encodeURIComponent(s.id)}`);
              }}
            >
              <CardContent
                sx={{
                  display: "flex",
                  flexDirection: "column",
                  alignItems: "center",
                  gap: 0.5,
                  justifyContent: "space-between",
                  height: "100%",
                  p: 2,
                }}
              >
                <Box sx={{ fontSize: 32 }}>{getSourceIcon(s.id)}</Box>
                <Typography
                  variant="caption"
                  noWrap
                  sx={{ textAlign: "center", maxWidth: 140 }}
                >
                  {s.name}
                </Typography>
              </CardContent>
            </Card>
          ))}
          {availableSources.length === 0 && (
            <Stack
              direction="row"
              spacing={1}
              alignItems="center"
              color="text.secondary"
            >
              <AddCircleOutline />
              <Typography variant="body2">
                {t("sources.noTypesAvailable")}
              </Typography>
            </Stack>
          )}
        </Box>
      </Box>
      <Snackbar
        open={snackbar !== null}
        autoHideDuration={6000}
        onClose={() => setSnackbar(null)}
        message={snackbar}
      />
    </Stack>
  );
}

export default SourcesPage;
