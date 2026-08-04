import { Button, Stack } from "@mui/material";
import { Login } from "@mui/icons-material";
import { useTranslation } from "react-i18next";
import type { PublicOidcProvider } from "../../types/auth";

interface OidcLoginButtonsProps {
  providers: PublicOidcProvider[];
  loadingSlug: string | null;
  onSelect: (provider: PublicOidcProvider) => Promise<void>;
}

export default function OidcLoginButtons({
  providers,
  loadingSlug,
  onSelect,
}: OidcLoginButtonsProps) {
  const { t } = useTranslation();

  return (
    <Stack spacing={1}>
      {providers.map((provider) => (
        <Button
          key={provider.slug}
          variant="outlined"
          startIcon={<Login />}
          disabled={loadingSlug !== null}
          onClick={() => onSelect(provider)}
          fullWidth
        >
          {loadingSlug === provider.slug
            ? t("auth.redirecting")
            : t("auth.continueWith", { provider: provider.name })}
        </Button>
      ))}
    </Stack>
  );
}
