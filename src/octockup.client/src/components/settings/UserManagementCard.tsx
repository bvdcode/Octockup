import { Add } from "@mui/icons-material";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  FormControlLabel,
  Stack,
  Switch,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuthApi } from "../../api/authApi";
import type {
  AdminUser,
  CreateUserRequest,
  UpdateUserAccessRequest,
} from "../../types/auth";
import { getApiErrorMessage } from "../../utils/apiError";
import CreateUserDialog from "./CreateUserDialog";

export default function UserManagementCard() {
  const { t } = useTranslation();
  const authApi = useAuthApi();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [createError, setCreateError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    authApi
      .listUsers()
      .then((loadedUsers) => {
        if (active) {
          setUsers(loadedUsers);
        }
      })
      .catch((caughtError) => {
        if (active && caughtError instanceof Error) {
          setError(getApiErrorMessage(caughtError, t("settings.loadFailed")));
        }
      });
    return () => {
      active = false;
    };
  }, [authApi, t]);

  const replaceUser = (updatedUser: AdminUser) => {
    setUsers((current) =>
      current.map((user) =>
        user.id === updatedUser.id ? updatedUser : user,
      ),
    );
  };

  const handleAccessChange = async (
    user: AdminUser,
    request: UpdateUserAccessRequest,
  ) => {
    setSavingId(user.id);
    setError(null);
    try {
      replaceUser(await authApi.updateUserAccess(user.id, request));
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("settings.saveFailed")));
      }
    } finally {
      setSavingId(null);
    }
  };

  const handleCreate = async (request: CreateUserRequest) => {
    setSavingId("create");
    setCreateError(null);
    try {
      const createdUser = await authApi.createUser(request);
      setUsers((current) => [...current, createdUser]);
      setCreateOpen(false);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setCreateError(
          getApiErrorMessage(caughtError, t("settings.saveFailed")),
        );
      }
    } finally {
      setSavingId(null);
    }
  };

  return (
    <Card>
      <CardContent>
        <Stack spacing={2}>
          <Stack
            direction={{ xs: "column", sm: "row" }}
            justifyContent="space-between"
            alignItems={{ xs: "stretch", sm: "center" }}
            spacing={1}
          >
            <Box>
              <Typography variant="h6">{t("settings.users.title")}</Typography>
              <Typography variant="body2" color="text.secondary">
                {t("settings.users.description")}
              </Typography>
            </Box>
            <Button
              variant="contained"
              startIcon={<Add />}
              onClick={() => {
                setCreateError(null);
                setCreateOpen(true);
              }}
            >
              {t("settings.users.create")}
            </Button>
          </Stack>
          {error && <Alert severity="error">{error}</Alert>}
          <Stack spacing={1}>
            {users.map((user) => (
              <Stack
                key={user.id}
                direction={{ xs: "column", sm: "row" }}
                alignItems={{ xs: "stretch", sm: "center" }}
                justifyContent="space-between"
                spacing={1}
                p={1}
                border={1}
                borderColor="divider"
                borderRadius={1}
              >
                <Box>
                  <Typography>{user.username}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {t("settings.users.linkedAccounts", {
                      count: user.externalIdentityCount,
                    })}
                  </Typography>
                </Box>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
                  <FormControlLabel
                    control={
                      <Checkbox
                        checked={user.isAdmin}
                        disabled={savingId !== null}
                        onChange={(event) =>
                          handleAccessChange(user, {
                            isAdmin: event.target.checked,
                            isDisabled: user.isDisabled,
                          })
                        }
                      />
                    }
                    label={t("settings.users.admin")}
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        checked={!user.isDisabled}
                        disabled={savingId !== null}
                        onChange={(event) =>
                          handleAccessChange(user, {
                            isAdmin: user.isAdmin,
                            isDisabled: !event.target.checked,
                          })
                        }
                      />
                    }
                    label={t("settings.users.enabled")}
                  />
                </Stack>
              </Stack>
            ))}
          </Stack>
        </Stack>
      </CardContent>

      {createOpen && (
        <CreateUserDialog
          open
          saving={savingId === "create"}
          error={createError}
          onClose={() => setCreateOpen(false)}
          onCreate={handleCreate}
        />
      )}
    </Card>
  );
}
