import { FC, useEffect, useState, useCallback } from "react";
import { Box, Paper, TextField, Typography } from "@mui/material";

interface IntervalInputProps {
  label?: string;
  onChange?: (interval: number) => void;
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

  const calculateInterval = useCallback(() => {
    const interval =
      days * 24 * 60 * 60 + hours * 60 * 60 + minutes * 60 + seconds;
    if (!isNaN(interval) && onChange) {
      onChange(interval);
    }
  }, [days, hours, minutes, seconds, onChange]);

  useEffect(() => {
    calculateInterval();
  }, [days, hours, minutes, seconds, calculateInterval]);

  const handleNumberChange = (value: string, setter: (val: number) => void) => {
    const num = parseInt(value);
    setter(isNaN(num) ? 0 : num);
  };

  return (
    <Box sx={{ position: "relative", display: "inline-block", width: "100%" }}>
      {label && (
        <Typography
          variant="subtitle1"
          sx={{
            position: "absolute",
            top: -10,
            left: 6,
            backgroundColor: "background.paper",
            padding: "0 4px",
            color: "text.secondary",
            fontSize: 12,
          }}
        >
          {label}
        </Typography>
      )}
      <Paper variant="outlined" sx={{ p: 2, pt: 4, bgcolor: "transparent" }}>
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
            onChange={(e) => handleNumberChange(e.target.value, setDays)}
            InputProps={{ inputProps: { min: 0, max: 365 } }}
          />
          <TextField
            label="Hours"
            type="number"
            value={hours}
            variant="outlined"
            onChange={(e) => handleNumberChange(e.target.value, setHours)}
            InputProps={{ inputProps: { min: 0, max: 23 } }}
          />
          <TextField
            label="Minutes"
            type="number"
            value={minutes}
            variant="outlined"
            onChange={(e) => handleNumberChange(e.target.value, setMinutes)}
            InputProps={{ inputProps: { min: 0, max: 59 } }}
          />
          <TextField
            label="Seconds"
            type="number"
            value={seconds}
            variant="outlined"
            onChange={(e) => handleNumberChange(e.target.value, setSeconds)}
            InputProps={{ inputProps: { min: 0, max: 59 } }}
          />
        </Box>
      </Paper>
    </Box>
  );
};

export default IntervalInput;
