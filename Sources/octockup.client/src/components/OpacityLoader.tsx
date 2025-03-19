import { Refresh } from "@mui/icons-material";
import React, { useEffect, useState } from "react";
import { Box, Button, LinearProgress, Paper, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";

interface OpacityLoaderProps {
  text?: string;
}

const OpacityLoader: React.FC<OpacityLoaderProps> = (props) => {
  const [elapsedTime, setElapsedTime] = React.useState(0);
  const [blurAmount, setBlurAmount] = useState(0);
  const { t } = useTranslation();
  const maxElapsedTime = 10;
  useEffect(() => {
    const interval = setInterval(() => {
      setElapsedTime((prev) => prev + 0.1);
    }, 100);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    setBlurAmount(0);
    const targetBlur = 16;
    const minBlur = 6;
    const animationDuration = 3000;
    const updateInterval = 50;
    const steps = animationDuration / updateInterval;
    const step = targetBlur / steps;
    let currentStep = 0;
    let increasing = true;

    const animation = setInterval(() => {
      if (increasing) {
        currentStep++;
        if (currentStep * step >= targetBlur) {
          increasing = false;
        }
      } else {
        currentStep--;
        if (currentStep * step <= minBlur) {
          increasing = true;
        }
      }
      setBlurAmount(currentStep * step);
    }, updateInterval);

    return () => clearInterval(animation);
  }, []);

  return (
    <Box
      sx={{
        position: "fixed",
        top: 0,
        left: 0,
        width: "100%",
        height: "100%",
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        flexDirection: "column",
        backdropFilter: `blur(${blurAmount}px)`,
        backgroundColor: `rgba(0, 0, 0, ${blurAmount * 0.01})`, // Опциональное затемнение вместе с размытием
        zIndex: 9999,
        gap: "20px",
        transition: "background-color 2s ease", // Плавный переход для фона
      }}
    >
      <Paper
        sx={{
          borderRadius: "15px",
          padding: "15px",
          boxShadow: "0 0px 100px rgba(0, 0, 0, 0.3)",
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          flexDirection: "column",
        }}
      >
        <Typography>{props.text ?? t("loading")}</Typography>
        <LinearProgress
          sx={{
            width: "100%",
          }}
          variant="query"
          color="primary"
        />
      </Paper>
      <Button
        sx={{
          height: "64px",
          borderRadius: "50%",
        }}
        variant="text"
        color="primary"
        onClick={() => (window.location.href = "/")}
        disabled={elapsedTime < maxElapsedTime}
        style={{
          opacity: elapsedTime < maxElapsedTime ? 0 : 1,
        }}
      >
        <Refresh />
      </Button>
    </Box>
  );
};

export default OpacityLoader;
