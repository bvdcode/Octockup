import {
  LogLevel,
  HubConnection,
  HubConnectionBuilder,
} from "@microsoft/signalr";
import { useEffect, useState } from "react";
import { useAuthStore } from "@bvdcode/react-kit";

export function useSignalR(hubUrl: string) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const apiService = useAuthStore((s) => s.apiService);
  const accessToken = useAuthStore((s) => s.accessToken);

  useEffect(() => {
    if (!apiService || !accessToken) {
      return;
    }

    let mounted = true;
    const newConnection = new HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => accessToken || "",
      })
      .configureLogging(LogLevel.None)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();
    newConnection
      .start()
      .then(() => {
        if (!mounted) return;
        setIsConnected(true);
        setConnection(newConnection);
      })
      .catch(() => {
        setIsConnected(false);
      });

    newConnection.onclose(() => {
      setIsConnected(false);
    });

    newConnection.onreconnecting(() => {
      setIsConnected(false);
    });

    newConnection.onreconnected(() => {
      setIsConnected(true);
    });

    return () => {
      mounted = false;
      // stop created connection and clear state only if it was the one we created
      // ignore errors on stop
      newConnection.stop().catch(() => {});
      setConnection((prev) => (prev === newConnection ? null : prev));
      setIsConnected(false);
    };
  }, [hubUrl, apiService, accessToken]);

  return { connection, isConnected };
}
