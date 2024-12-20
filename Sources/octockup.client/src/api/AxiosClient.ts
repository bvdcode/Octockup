import axios from "axios";
import { API_BASE_URL } from "../config";
import EventEmitter from "../handlers/EventEmitter";

class AxiosClient {
  private static instance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
      "Content-Type": "application/json",
    },
  });

  static events = new EventEmitter();

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
      AxiosClient.events.emit("logout");
    }
    return Promise.reject(error);
  }
);

export default AxiosClient;
