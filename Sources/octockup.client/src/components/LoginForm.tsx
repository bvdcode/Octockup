import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Box, Button, TextField, Typography } from "@mui/material";

interface LoginFormProps {
  onLogin: (username: string, password: string) => void;
}

const LoginForm: React.FC<LoginFormProps> = ({ onLogin }) => {
  const { t } = useTranslation();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onLogin(username, password);
  };

  return (
    <Box
      display="flex"
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      width="100%"
      height="100%"
    >
      <form
        onSubmit={handleSubmit}
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "16px",
          maxWidth: "400px",
          margin: "0 auto",
        }}
      >
        <Typography variant="h4" align="center" gutterBottom>
          Octockup
        </Typography>
        <TextField
          type="text"
          variant="standard"
          placeholder={t("login.username")}
          value={username}
          required
          onChange={(e) => setUsername(e.target.value)}
          fullWidth
        />
        <TextField
          type="password"
          variant="standard"
          placeholder={t("login.password")}
          value={password}
          required
          onChange={(e) => setPassword(e.target.value)}
          fullWidth
        />
        <Button
          variant="contained"
          color="primary"
          type="submit"
          sx={{ mt: 2 }}
          disabled={!username || !password}
          onClick={handleSubmit}
          fullWidth
        >
          {t("login.login")}
        </Button>
      </form>
    </Box>
  );
};

export default LoginForm;
