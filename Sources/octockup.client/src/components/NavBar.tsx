import { Box, Button, Paper } from "@mui/material";
import { useNavigate } from "react-router-dom";
import React, { useState, useEffect } from "react";

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
      sx={{
        position: "fixed",
        bottom: 0,
        left: "50%",
        transform: "translateX(-50%)",
        width: "100%",
        maxWidth: "300px",
        height: "30px",
        zIndex: 1000,
      }}
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
    >
      <Paper
        sx={{
          position: "absolute",
          bottom: isVisible ? 0 : "-70px",
          left: "50%",
          transform: "translateX(-50%)",
          width: "100%",
          height: "80px",
          borderTopLeftRadius: "40px",
          borderTopRightRadius: "40px",
          display: "flex",
          justifyContent: "space-around",
          alignItems: "center",
          boxShadow: "0 -2px 10px rgba(0, 0, 0, 0.2)",
          transition: "bottom 0.3s ease",
          "&::before": {
            content: '""',
            position: "absolute",
            top: 0,
            left: "50%",
            transform: "translateX(-50%)",
            width: "50px",
            height: "4px",
            borderRadius: "2px",
          },
        }}
      >
        {props.buttons.map((button, index) => (
          <Button
            key={index}
            sx={{ justifyContent: "center" }}
            onClick={() => navigate(button.path)}
          >
            {button.icon}
          </Button>
        ))}
      </Paper>
    </Box>
  );
};

export default NavBar;
