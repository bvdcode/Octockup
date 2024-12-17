export const API_BASE_URL =
  // if current url contains "localhost", use the local backend
  window.location.href.includes("localhost")
    ? "http://localhost:5112/api/v1"
    : "/api/v1";
export const LOCAL_STORAGE_REFRESH_TOKEN_KEY = "OCTOCKUP_REFRESH_TOKEN";
