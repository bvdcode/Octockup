import React, { useState, useEffect } from "react";
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
  const [isHovered, setIsHovered] = useState(false);
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    let timer: NodeJS.Timeout;
    if (isHovered) {
      setIsVisible(true);
    } else {
      timer = setTimeout(() => {
        setIsVisible(false);
      }, 500);
    }
    return () => clearTimeout(timer);
  }, [isHovered]);

  return (
    <Box
      className={styles.navbarContainer}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <Box className={`${styles.navbar} ${isVisible ? styles.visible : ""}`}>
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
