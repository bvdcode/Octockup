import { Box, TextField } from "@mui/material";
import { useEffect, useState } from "react";

interface IntervalInputProps {
  value?: number;
  label?: string;
  fullWidth?: boolean;
  margin?: "none" | "dense" | "normal";
  variant?: "standard" | "outlined" | "filled";
  onChange?: (seconds: number) => void;
}

const IntervalInput: React.FC<IntervalInputProps> = ({
  value,
  label,
  fullWidth,
  margin,
  variant,
  onChange,
}) => {
  const [days, setDays] = useState<number>(
    Math.floor(value ? value / (60 * 60 * 24) : 0)
  );
  const [hours, setHours] = useState<number>(
    Math.floor(value ? (value % (60 * 60 * 24)) / (60 * 60) : 0)
  );
  const [minutes, setMinutes] = useState<number>(
    Math.floor(value ? (value % (60 * 60)) / 60 : 0)
  );
  const [seconds, setSeconds] = useState<number>(
    Math.floor(value ? value % 60 : 0)
  );

  useEffect(() => {
    const newTotalSeconds =
      days * 60 * 60 * 24 + hours * 60 * 60 + minutes * 60 + seconds;
    onChange?.(newTotalSeconds);
  }, [days, hours, minutes, seconds, onChange]);

  return (
    <Box sx={{ display: "flex", gap: 2 }}>
      <TextField
        label="Days"
        value={days}
        onChange={(e) => setDays(parseInt(e.target.value))}
      />
      <TextField
        label="Hours"
        value={hours}
        onChange={(e) => setHours(parseInt(e.target.value))}
      />
      <TextField
        label="Minutes"
        value={minutes}
        onChange={(e) => setMinutes(parseInt(e.target.value))}
      />
      <TextField
        label="Seconds"
        value={seconds}
        onChange={(e) => setSeconds(parseInt(e.target.value))}
      />
    </Box>
  );
};

export default IntervalInput;
