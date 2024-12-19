import React from "react";
import { useNavigate } from "react-router-dom";
import { Box, Button } from "@mui/material";
import styles from "./NavBar.module.css";

interface NavButton {
  path: string;
  icon: React.ReactNode;
}

interface NavBarProps {
  buttons: NavButton[];
}

const NavBar: React.FC<NavBarProps> = (props) => {
  const navigate = useNavigate();

  return (
    <Box className={styles.navbarContainer}>
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
    </Box>
  );
};

export default NavBar;
