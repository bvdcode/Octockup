import { FC, useEffect, useState } from "react";
import { Box, Paper, TextField, Typography } from "@mui/material";

interface IntervalInputProps {
  label?: string;
  onChange: (interval: number) => void;
  defaultValue?: number;
}

const IntervalInput: FC<IntervalInputProps> = ({
  label,
  onChange,
  defaultValue,
}) => {
  const [days, setDays] = useState(
    defaultValue ? Math.floor(defaultValue / (24 * 60 * 60)) : 0
  );
  const [hours, setHours] = useState(
    defaultValue ? Math.floor((defaultValue % (24 * 60 * 60)) / (60 * 60)) : 0
  );
  const [minutes, setMinutes] = useState(
    defaultValue ? Math.floor((defaultValue % (60 * 60)) / 60) : 0
  );
  const [seconds, setSeconds] = useState(defaultValue ? defaultValue % 60 : 0);

  useEffect(() => {
    onChange(days * 24 * 60 * 60 + hours * 60 * 60 + minutes * 60 + seconds);
  }, [days, hours, minutes, seconds, onChange]);

  return (
    <Paper variant="outlined" sx={{ p: 2, bgcolor: "transparent" }}>
      {label && (
        <Typography variant="subtitle1" mb={2} color="text.secondary">
          {label}
        </Typography>
      )}
      <Box
        sx={{
          display: "flex",
          gap: 2,
          "& .MuiTextField-root": {
            flex: 1,
          },
        }}
      >
        <TextField
          label="Days"
          type="number"
          value={days}
          variant="outlined"
          onChange={(e) => setDays(parseInt(e.target.value))}
          InputProps={{ inputProps: { min: 0, max: 365 } }}
        />
        <TextField
          label="Hours"
          type="number"
          value={hours}
          variant="outlined"
          onChange={(e) => setHours(parseInt(e.target.value))}
          InputProps={{ inputProps: { min: 0, max: 23 } }}
        />
        <TextField
          label="Minutes"
          type="number"
          value={minutes}
          variant="outlined"
          onChange={(e) => setMinutes(parseInt(e.target.value))}
          InputProps={{ inputProps: { min: 0, max: 59 } }}
        />
        <TextField
          label="Seconds"
          type="number"
          value={seconds}
          variant="outlined"
          onChange={(e) => setSeconds(parseInt(e.target.value))}
          InputProps={{ inputProps: { min: 0, max: 59 } }}
        />
      </Box>
    </Paper>
  );
};

export default IntervalInput;
