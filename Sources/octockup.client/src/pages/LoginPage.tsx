import { useState } from "react";
import { login } from "../api/api";
import { Paper } from "@mui/material";
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { LoginForm } from "../components";
import { useTranslation } from "react-i18next";
import { Navigate, useNavigate } from "react-router-dom";

const LoginPage: React.FC = () => {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { isAuthenticated } = useAuth();
  const [isLoading, setIsLoading] = useState(false);

  if (isAuthenticated) {
    return <Navigate to="/" />;
  }

  const onLogin = (email: string, password: string) => {
    setIsLoading(true);
    login(email, password)
      .then((response) => {
        signIn({
          auth: {
            token: response.accessToken,
          },
          refresh: response.refreshToken,
          userState: response.user,
        });
        toast.success(t("login.success"));
        navigate("/");
      })
      .catch((error) => {
        toast.error(t("login.error", { error: error.message }));
      })
      .finally(() => {
        setIsLoading(false);
      });
  };
  return (
    <Paper sx={{ borderRadius: 0, height: "100%", width: "100%" }}>
      <LoginForm onLogin={onLogin} isLoading={isLoading} />
    </Paper>
  );
};

export default LoginPage;
