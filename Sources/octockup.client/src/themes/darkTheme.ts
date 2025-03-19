import { createTheme } from "@mui/material";

export const darkTheme = createTheme({
  palette: {
    mode: "dark",
    primary: {
      main: "rgb(253, 194, 255)",
    },
    secondary: {
      main: "rgb(244, 143, 177)",
    },
    background: {
      default: "rgba(42, 42, 42, 0.9)",
      paper: "rgba(54, 54, 54, 0.9)",
    },
    text: {
      primary: "rgb(210, 210, 210)",
      secondary: "rgb(200, 200, 200)",
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
          -webkit-box-shadow: 0 0 0 30px rgba(64, 64, 64, 0.9) inset !important;
        }
        
        .app {
          background: linear-gradient(rgba(0, 0, 0, 0.8), rgba(0, 0, 0, 0.7)), url("/assets/images/pig-bg.png");
          background-repeat: repeat;
          background-size: 40%;
        }
      `,
    },
  },
});
