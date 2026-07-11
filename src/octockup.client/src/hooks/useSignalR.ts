import {
  LogLevel,
  HubConnection,
  HubConnectionBuilder,
} from "@microsoft/signalr";
import { useEffect, useState } from "react";
import { useAuthStore, useAxios } from "@bvdcode/react-kit";
import { SignalRConnectionManager } from "../utils/SignalRConnectionManager";

export function useSignalR(hubUrl: string) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const apiService = useAuthStore((s) => s.apiService);
  const accessToken = useAuthStore((s) => s.accessToken);
  const axios = useAxios();

  useEffect(() => {
    if (!apiService || !accessToken) {
      return;
    }

    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => accessToken || "",
      })
      .configureLogging(LogLevel.None)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    const manager = new SignalRConnectionManager(
      newConnection,
      {
        onConnected: () => {
          setIsConnected(true);
          setConnection(newConnection);
        },
        onDisconnected: () => {
          setIsConnected(false);
        },
        refreshAuthorization: async () => {
          await axios.get("/api/v1/auth/me");
        },
      },
      {
        setTimeout: (callback, delayMs) => window.setTimeout(callback, delayMs),
        clearTimeout: (timerId) => window.clearTimeout(timerId),
      },
    );
    manager.start();

    return () => {
      void manager.dispose();
      setConnection((prev) => (prev === newConnection ? null : prev));
      setIsConnected(false);
    };
  }, [hubUrl, apiService, accessToken, axios]);

  return { connection, isConnected };
}
