import { useEffect, useState } from "react";
import { useSignalR } from "../hooks/useSignalR";
import { Box, Typography, Card, CardContent } from "@mui/material";

export function HomePage() {
  const [serverTime, setServerTime] = useState<string>("");
  const { connection, isConnected } = useSignalR("/api/v1/event-hub");

  useEffect(() => {
    if (!connection || !isConnected) return;

    connection.on("Time", (utcTime: string) => {
      setServerTime(utcTime);
    });

    return () => {
      connection.off("Time");
    };
  }, [connection, isConnected]);

  function formatLocalWithMs(utcString: string | undefined | null) {
    if (!utcString) return "";
    const d = new Date(utcString);
    if (isNaN(d.getTime())) return utcString;
    const datePart = d.toLocaleDateString();
    const timePart = d.toLocaleTimeString(undefined, {
      hour12: false,
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
    const ms = d.getMilliseconds().toString().padStart(3, "0");
    return `${datePart} ${timePart}.${ms}`;
  }

  return (
    <Box>
      <Typography variant="h4" gutterBottom>
        Home
      </Typography>
      <Card sx={{ mt: 2 }}>
        <CardContent>
          <Typography variant="subtitle1" gutterBottom>
            Server Time
          </Typography>
          <Typography
            variant="h6"
            color={isConnected ? "primary" : "text.secondary"}
          >
            {isConnected
              ? serverTime
                ? formatLocalWithMs(serverTime)
                : "Waiting for time..."
              : "Not connected"}
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
}

export default HomePage;
