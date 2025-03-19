import { useState } from "react";
import { login } from "../api/api";
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { useTranslation } from "react-i18next";
import { LoginForm, OpacityLoader } from "../components";
import { Navigate, useNavigate } from "react-router-dom";

const LoginPage: React.FC = () => {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { isAuthenticated } = useAuth();
  const [isLoading, setIsLoading] = useState(false);

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
    <>
      {isLoading && <OpacityLoader text={t("login.loader")} />}
      <LoginForm onLogin={onLogin} />
      {isAuthenticated && <Navigate to="/" />}
    </>
  );
};

export default LoginPage;
