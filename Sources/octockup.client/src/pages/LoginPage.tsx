import { LoginForm } from "../components";
import { useNavigate } from "react-router-dom";
import { handleLogin } from "../services/authService";

const LoginPage: React.FC = () => {
  const navigate = useNavigate();

  const onLogin = async (username: string, password: string) => {
    const success = await handleLogin(username, password);
    if (success) {
      navigate("/");
    } else {
      alert("Login failed");
    }
  };

  return <LoginForm onLogin={onLogin} />;
};

export default LoginPage;
