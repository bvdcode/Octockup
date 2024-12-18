import { useState } from "react";
import styles from "./LoginForm.module.css";
import { Button, Input } from "@mui/material";
import { useTranslation } from "react-i18next";

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
    <form onSubmit={handleSubmit} className={styles.form}>
      <h1>Octockup</h1>
      <Input
        type="text"
        className={styles.input}
        placeholder={t("login.username")}
        value={username}
        required
        onChange={(e) => setUsername(e.target.value)}
      />
      <Input
        type="password"
        className={styles.input}
        placeholder={t("login.password")}
        value={password}
        required
        onChange={(e) => setPassword(e.target.value)}
      />
      <Button type="submit">{t("login.login")}</Button>
    </form>
  );
};

export default LoginForm;
