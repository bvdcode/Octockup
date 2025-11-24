import { useEffect, useState } from "react";
import { useSignalR } from "../hooks/useSignalR";
import { Box, Typography, Card, CardContent } from "@mui/material";

export function HomePage() {
  const [serverTime, setServerTime] = useState<string | null>(null);
  const { connection, isConnected } = useSignalR("http://localhost:5112/api/v1/event-hub");

  useEffect(() => {
    if (!connection || !isConnected) return;

    connection.on("Time", (utcTime: string) => {
      setServerTime(utcTime);
    });

    return () => {
      connection.off("Time");
    };
  }, [connection, isConnected]);

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>
        Home
      </Typography>
      <Card variant="outlined" sx={{ mt: 2 }}>
        <CardContent>
          <Typography variant="subtitle1" gutterBottom>
            Server Time (UTC)
          </Typography>
          <Typography
            variant="h6"
            color={isConnected ? "primary" : "text.secondary"}
          >
            {isConnected
              ? serverTime || "Waiting for time..."
              : "Not connected"}
          </Typography>
        </CardContent>
      </Card>
    </Box>
  );
}

export default HomePage;
