import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography,
} from "@mui/material";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAuthStore } from "@bvdcode/react-kit";
import { AuthApiClient } from "../api/authApi";
import LoginMethods from "../components/auth/LoginMethods";
import type {
  AuthenticationOptions,
  OidcCallbackStatus,
  PublicOidcProvider,
  RefreshSessionResponse,
} from "../types/auth";
import {
  clearOidcCallbackStatus,
  getCurrentReturnUrl,
  getOidcCallbackStatus,
} from "../utils/authUtils";
import { getApiErrorMessage } from "../utils/apiError";

type LoginInitializationErrorKey =
  | "auth.externalSignInFailed"
  | "auth.optionsFailed"
  | null;

interface LoginInitializationResult {
  options: AuthenticationOptions | null;
  session: RefreshSessionResponse | null;
  error: Error | null;
  errorKey: LoginInitializationErrorKey;
}

async function initializeLogin(
  authApi: AuthApiClient,
  callbackStatus: OidcCallbackStatus,
): Promise<LoginInitializationResult> {
  let error: Error | null = null;
  let errorKey: LoginInitializationErrorKey = null;

  if (callbackStatus === "error") {
    errorKey = "auth.externalSignInFailed";
    clearOidcCallbackStatus();
  }

  if (callbackStatus === "success" || callbackStatus === "linked") {
    try {
      const session = await authApi.refreshFromCookie();
      clearOidcCallbackStatus();
      return { options: null, session, error: null, errorKey: null };
    } catch (caughtError) {
      error = caughtError instanceof Error ? caughtError : null;
      errorKey = "auth.externalSignInFailed";
      clearOidcCallbackStatus();
    }
  }

  try {
    const options = await authApi.getOptions();
    return { options, session: null, error, errorKey };
  } catch (caughtError) {
    if (errorKey === null) {
      error = caughtError instanceof Error ? caughtError : null;
      errorKey = "auth.optionsFailed";
    }
    return { options: null, session: null, error, errorKey };
  }
}

export default function LoginPage() {
  const { t } = useTranslation();
  const apiService = useAuthStore((state) => state.apiService);
  const login = useAuthStore((state) => state.login);
  const setAccessToken = useAuthStore((state) => state.setAccessToken);
  const setRefreshToken = useAuthStore((state) => state.setRefreshToken);
  const initialization = useRef<Promise<LoginInitializationResult> | null>(
    null,
  );
  const [initializationAttempt, setInitializationAttempt] = useState(0);
  const [initializing, setInitializing] = useState(true);
  const [options, setOptions] = useState<AuthenticationOptions | null>(null);
  const [loadingPassword, setLoadingPassword] = useState(false);
  const [loadingSlug, setLoadingSlug] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (apiService === null) {
      return;
    }

    let active = true;
    if (initialization.current === null) {
      const authApi = new AuthApiClient(apiService.getAxios());
      const callbackStatus = getOidcCallbackStatus(window.location.search);
      initialization.current = initializeLogin(authApi, callbackStatus);
    }

    void initialization.current.then((result) => {
      if (!active) {
        return;
      }
      if (result.options !== null) {
        setOptions(result.options);
        setInitializing(false);
      }
      if (result.session !== null) {
        setRefreshToken(result.session.refreshToken);
        setAccessToken(result.session.accessToken);
      }
      if (result.errorKey !== null) {
        const fallback = t(result.errorKey);
        setError(
          result.error === null
            ? fallback
            : getApiErrorMessage(result.error, fallback),
        );
        setInitializing(false);
      }
    });
    return () => {
      active = false;
    };
  }, [apiService, initializationAttempt, setAccessToken, setRefreshToken, t]);

  const handleInitializationRetry = () => {
    initialization.current = null;
    setError(null);
    setInitializing(true);
    setInitializationAttempt((currentAttempt) => currentAttempt + 1);
  };

  const handlePasswordLogin = async (username: string, password: string) => {
    setLoadingPassword(true);
    setError(null);
    try {
      await login({ username, password });
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("auth.invalidCredentials")));
      }
    } finally {
      setLoadingPassword(false);
    }
  };

  const handleOidcLogin = async (provider: PublicOidcProvider) => {
    if (apiService === null) {
      return;
    }

    setLoadingSlug(provider.slug);
    setError(null);
    try {
      const authApi = new AuthApiClient(apiService.getAxios());
      const authorizationUrl = await authApi.beginOidcAuthorization(
        provider.slug,
        getCurrentReturnUrl(),
        false,
      );
      window.location.assign(authorizationUrl);
    } catch (caughtError) {
      if (caughtError instanceof Error) {
        setError(getApiErrorMessage(caughtError, t("auth.externalSignInFailed")));
      }
      setLoadingSlug(null);
    }
  };

  return (
    <Box
      minHeight="100%"
      display="flex"
      alignItems="center"
      justifyContent="center"
      p={2}
    >
      <Card sx={{ width: "100%", maxWidth: 420 }}>
        <CardContent>
          <Stack spacing={3}>
            <Stack alignItems="center" spacing={1}>
              <Box component="img" src="/octockup.png" alt="" width={72} />
              <Typography variant="h4">Octockup</Typography>
              <Typography color="text.secondary">
                {t("auth.title")}
              </Typography>
            </Stack>

            {error && <Alert severity="error">{error}</Alert>}

            {initializing && (
              <Box display="flex" justifyContent="center" p={2}>
                <CircularProgress />
              </Box>
            )}
            {!initializing && options !== null && (
              <LoginMethods
                options={options}
                loadingPassword={loadingPassword}
                loadingSlug={loadingSlug}
                onPasswordLogin={handlePasswordLogin}
                onOidcLogin={handleOidcLogin}
              />
            )}
            {!initializing && options === null && (
              <Button variant="outlined" onClick={handleInitializationRetry}>
                {t("common.retry")}
              </Button>
            )}
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}
