import {
  LogLevel,
  HubConnection,
  HubConnectionBuilder,
} from "@microsoft/signalr";
import { useEffect, useRef, useState } from "react";
import { useAuthStore } from "@bvdcode/react-kit";

export function useSignalR(hubUrl: string) {
  const [connection, setConnection] = useState<HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const apiService = useAuthStore((s) => s.apiService);
  const accessToken = useAuthStore((s) => s.accessToken);

  const retryIndexRef = useRef(0);
  const retryTimerRef = useRef<number | null>(null);
  const retryDelays = [0, 2000, 5000, 10000, 30000];

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

    const startWithRetry = () => {
      if (!mounted) return;
      newConnection
        .start()
        .then(() => {
          if (!mounted) return;
          retryIndexRef.current = 0;
          setIsConnected(true);
          setConnection(newConnection);
        })
        .catch(() => {
          // If initial start fails (e.g., server down), retry with backoff
          setIsConnected(false);
          const i = retryIndexRef.current;
          const delay = retryDelays[Math.min(i, retryDelays.length - 1)];
          retryIndexRef.current = i + 1;
          if (retryTimerRef.current) {
            clearTimeout(retryTimerRef.current);
          }
          retryTimerRef.current = setTimeout(() => {
            startWithRetry();
          }, delay) as unknown as number;
        });
    };

    startWithRetry();

    newConnection.onclose(() => {
      setIsConnected(false);
      // After a hard close, SignalR will try via withAutomaticReconnect only if it was started once
      // In case it gives up, try to start again using our loop
      if (mounted) {
        startWithRetry();
      }
    });

    newConnection.onreconnecting(() => {
      setIsConnected(false);
    });

    newConnection.onreconnected(() => {
      setIsConnected(true);
    });

    return () => {
      mounted = false;
      if (retryTimerRef.current) {
        clearTimeout(retryTimerRef.current);
        retryTimerRef.current = null;
      }
      // stop created connection and clear state only if it was the one we created
      // ignore errors on stop
      newConnection.stop().catch(() => {});
      setConnection((prev) => (prev === newConnection ? null : prev));
      setIsConnected(false);
    };
  }, [hubUrl, apiService, accessToken]);

  return { connection, isConnected };
}
