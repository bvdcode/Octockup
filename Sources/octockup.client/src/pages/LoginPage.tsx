import { Navigate } from "react-router-dom";
import { login } from "../api/api";
import { LoginForm } from "../components";
import useSignIn from "react-auth-kit/hooks/useSignIn";

const LoginPage: React.FC = () => {
  const signIn = useSignIn();

  const onLogin = (email: string, password: string) => {
    login(email, password).then((response) => {
      if (
        signIn({
          auth: {
            token: response.accessToken,
            type: "Bearer",
          },
          refresh: response.refreshToken,
          userState: {
            name: "React User",
            uid: 123456,
          },
        })
      ) {
        // redirect to home page
        return <Navigate to="/" />;
      } else {
        alert("Login failed");
      }
    });
  };
  return <LoginForm onLogin={onLogin} />;
};

export default LoginPage;
