import { createTheme } from "@mui/material";

export const lightTheme = createTheme({
  palette: {
    mode: "light",
    primary: {
      main: "rgb(112, 0, 107)",
    },
    secondary: {
      main: "rgb(255, 255, 255)",
    },
    background: {
      default: "rgba(255, 255, 255, 0.98)",
      paper: "rgba(255, 255, 255, 0.98)",
    },
    text: {
      primary: "rgb(48, 48, 48)",
      secondary: "rgb(64, 64, 64)",
    },
  },
  typography: {
    fontFamily: '"Ubuntu", serif',
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: `
        input:-webkit-autofill,
        input:-webkit-autofill:hover,
        input:-webkit-autofill:focus,
        input:-webkit-autofill:active {
          -webkit-box-shadow: 0 0 0 30px rgba(255, 255, 255, 0.95) inset !important;
        }
        
        .app {
          background: linear-gradient(rgba(255, 255, 255, 0), rgba(192, 192, 192, 0.7)), url("/assets/images/pig-bg.png");
          background-repeat: repeat;
          background-size: 40%;
        }
      `,
    },
  },
});
