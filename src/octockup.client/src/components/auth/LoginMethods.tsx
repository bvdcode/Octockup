import { Alert, Divider, Stack } from "@mui/material";
import { useTranslation } from "react-i18next";
import type {
  AuthenticationOptions,
  PublicOidcProvider,
} from "../../types/auth";
import OidcLoginButtons from "./OidcLoginButtons";
import PasswordLoginForm from "./PasswordLoginForm";

interface LoginMethodsProps {
  options: AuthenticationOptions;
  loadingPassword: boolean;
  loadingSlug: string | null;
  onPasswordLogin: (username: string, password: string) => Promise<void>;
  onOidcLogin: (provider: PublicOidcProvider) => Promise<void>;
}

export default function LoginMethods({
  options,
  loadingPassword,
  loadingSlug,
  onPasswordLogin,
  onOidcLogin,
}: LoginMethodsProps) {
  const { t } = useTranslation();
  const hasPasswordLogin = options.passwordLoginEnabled;
  const hasOidcLogin = options.oidcProviders.length > 0;

  return (
    <Stack spacing={2}>
      {hasPasswordLogin && (
        <PasswordLoginForm
          loading={loadingPassword}
          onSubmit={onPasswordLogin}
        />
      )}
      {hasPasswordLogin && hasOidcLogin && <Divider>{t("auth.or")}</Divider>}
      {hasOidcLogin && (
        <OidcLoginButtons
          providers={options.oidcProviders}
          loadingSlug={loadingSlug}
          onSelect={onOidcLogin}
        />
      )}
      {!hasPasswordLogin && !hasOidcLogin && (
        <Alert severity="warning">{t("auth.noLoginMethods")}</Alert>
      )}
    </Stack>
  );
}
