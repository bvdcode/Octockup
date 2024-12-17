import { useNavigate } from "react-router-dom";
import { login } from "../api/api";
import { LoginForm } from "../components";
import useSignIn from "react-auth-kit/hooks/useSignIn";
import { toast } from "react-toastify";

const LoginPage: React.FC = () => {
  const signIn = useSignIn();
  const navigate = useNavigate();

  const onLogin = (email: string, password: string) => {
    login(email, password)
      .then((response) => {
        console.log("response :>> ", response);
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
