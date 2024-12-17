import axios from "axios";
import { API_BASE_URL } from "../config";

class ApiClient {
  private static instance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
      "Content-Type": "application/json",
    },
  });

  static setAccessToken(token: string | null) {
    if (token) {
      this.instance.defaults.headers.common[
        "Authorization"
      ] = `Bearer ${token}`;
    } else {
      delete this.instance.defaults.headers.common["Authorization"];
    }
  }

  static getInstance() {
    return this.instance;
  }
}

export default ApiClient;
