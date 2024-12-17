import axios from "axios";
import { toast } from "react-toastify";
import { API_BASE_URL } from "../config";
import useSignOut from "react-auth-kit/hooks/useSignOut";

class AxiosClient {
  private static instance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
      "Content-Type": "application/json",
    },
  });

  static setAuthHeader(header: string | null) {
    if (header) {
      this.instance.defaults.headers.common["Authorization"] = header;
    } else {
      delete this.instance.defaults.headers.common["Authorization"];
    }
  }

  static getInstance() {
    return this.instance;
  }
}

AxiosClient.getInstance().interceptors.response.use(
  (response) => response,
  (error) => {
    if (
      error.response &&
      error.response.status === 401 &&
      AxiosClient.getInstance().defaults.headers.common["Authorization"]
    ) {
      toast.error("Session expired. Please sign in again.");
      const signOut = useSignOut();
      signOut();
    }
    return Promise.reject(error);
  }
);

export default AxiosClient;
