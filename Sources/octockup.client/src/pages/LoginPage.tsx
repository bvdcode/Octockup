import { login } from "../api/api";
import { toast } from "react-toastify";
import { LoginForm } from "../components";
import { useNavigate } from "react-router-dom";
import useSignIn from "react-auth-kit/hooks/useSignIn";

const LoginPage: React.FC = () => {
  const signIn = useSignIn();
  const navigate = useNavigate();

  const onLogin = (email: string, password: string) => {
    login(email, password)
      .then((response) => {
        const signedIn = signIn({
          auth: {
            token: response.accessToken,
            type: "Bearer",
          },
          refresh: response.refreshToken,

          userState: {
            name: "React User",
            id: 1,
          },
        });
        if (signedIn) {
          toast.success("Login successful");
          navigate("/");
        } else {
          toast.error("Login failed");
        }
      })
      .catch((error) => {
        toast.error("Login failed: " + error.message);
      });
  };
  return <LoginForm onLogin={onLogin} />;
};

export default LoginPage;
