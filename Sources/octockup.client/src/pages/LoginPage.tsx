import { LoginForm } from "../components";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

const LoginPage: React.FC = () => {
  const navigate = useNavigate();
  const { login } = useAuth();

  const onLogin = async (username: string, password: string) => {
    const success = await login(username, password);
    if (success) {
      navigate("/");
    } else {
      alert("Login failed");
    }
  };

  return <LoginForm onLogin={onLogin} />;
};

export default LoginPage;
