import { login } from "../api/api";
import { toast } from "react-toastify";
import { LoginForm } from "../components";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import useSignIn from "react-auth-kit/hooks/useSignIn";

const LoginPage: React.FC = () => {
  const signIn = useSignIn();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const onLogin = (email: string, password: string) => {
    login(email, password)
      .then((response) => {
        const signedIn = signIn({
          auth: {
            token: response.accessToken,
            type: "Bearer",
          },
          refresh: response.refreshToken,
          userState: response.user,
        });
        if (signedIn) {
          toast.success(t("login.success"));
          navigate("/");
        } else {
          toast.error(t("login.failed"));
        }
      })
      .catch((error) => {
        toast.error(t("login.error", { error: error.message }));
      });
  };
  return <LoginForm onLogin={onLogin} />;
};

export default LoginPage;
