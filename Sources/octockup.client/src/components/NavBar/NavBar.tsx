import { Button } from "@mui/material";
import { useNavigate } from "react-router-dom";

interface NavButton {
  path: string;
  icon: JSX.Element;
}

interface NavBarProps {
  buttons: NavButton[];
}

const NavBar: React.FC<NavBarProps> = (props) => {
  const navigate = useNavigate();

  return (
    <div>
      {props.buttons.map((button, index) => (
        <Button
          key={index}
          sx={{ justifyContent: "center" }}
          onClick={() => navigate(button.path)}
        >
          {button.icon}
        </Button>
      ))}
    </div>
  );
};

export default NavBar;
