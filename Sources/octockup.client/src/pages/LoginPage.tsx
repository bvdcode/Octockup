import { LoginForm } from "../components";

const LoginPage: React.FC = () => {
  const onLogin = (email: string, password: string) => {
    console.log(`Email: ${email}, Password: ${password}`);
  };
  return <LoginForm onLogin={onLogin} />;
};

export default LoginPage;
