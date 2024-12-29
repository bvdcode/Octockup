import { useState } from "react";
import { login } from "../api/api";
import useAuth from "../auth/useAuth";
import { toast } from "react-toastify";
import { LoginForm } from "../components";
import Loader from "../components/Loader";
import { Navigate, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

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
      {isLoading && <Loader />}
      <LoginForm onLogin={onLogin} />
      {isAuthenticated && <Navigate to="/" />}
    </>
  );
};

export default LoginPage;
