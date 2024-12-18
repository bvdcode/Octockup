import { Box, Button } from "@mui/material";
import styles from "./NavBar.module.css";
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
    <Box className={styles.navbar}>
      {props.buttons.map((button, index) => (
        <Button
          key={index}
          sx={{ justifyContent: "center" }}
          onClick={() => navigate(button.path)}
        >
          {button.icon}
        </Button>
      ))}
    </Box>
  );
};

export default NavBar;
