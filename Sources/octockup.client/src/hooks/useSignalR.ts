import { useEffect, useState } from "react";
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from "@microsoft/signalr";
import { useAuthStore } from "@bvdcode/react-kit";

export function useSignalR(hubUrl: string) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const apiService = useAuthStore((s) => s.apiService);

  useEffect(() => {
    if (!apiService) return;

    const accessToken = apiService.getAccessToken();
    if (!accessToken) return;

    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => accessToken,
      })
      .configureLogging(LogLevel.None)
      .withAutomaticReconnect()
      .build();

    newConnection
      .start()
      .then(() => {
        setIsConnected(true);
        setConnection(newConnection);
      })
      .catch(() => {
        setIsConnected(false);
      });

    newConnection.onclose(() => {
      setIsConnected(false);
    });

    newConnection.onreconnected(() => {
      setIsConnected(true);
    });

    return () => {
      newConnection.stop();
      setConnection(null);
    };
  }, [hubUrl, apiService]);

  return { connection, isConnected };
}
