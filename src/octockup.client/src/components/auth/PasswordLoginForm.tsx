import { Button, Stack, TextField } from "@mui/material";
import { useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";

interface PasswordLoginFormProps {
  loading: boolean;
  onSubmit: (username: string, password: string) => Promise<void>;
}

export default function PasswordLoginForm({
  loading,
  onSubmit,
}: PasswordLoginFormProps) {
  const { t } = useTranslation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await onSubmit(username.trim(), password);
  };

  return (
    <Stack component="form" spacing={2} onSubmit={handleSubmit}>
      <TextField
        autoComplete="username"
        label={t("auth.username")}
        value={username}
        onChange={(event) => setUsername(event.target.value)}
        required
        fullWidth
      />
      <TextField
        autoComplete="current-password"
        label={t("auth.password")}
        type="password"
        value={password}
        onChange={(event) => setPassword(event.target.value)}
        required
        fullWidth
      />
      <Button
        type="submit"
        variant="contained"
        disabled={loading || !username.trim() || !password}
        fullWidth
      >
        {loading ? t("auth.signingIn") : t("auth.signIn")}
      </Button>
    </Stack>
  );
}
