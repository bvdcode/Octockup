import { Box } from "@mui/material";
import { ProgressBarColor } from "./ProgressBarColor";

interface ProgressBarProps {
  value: number;
  color?: ProgressBarColor;
}

const ProgressBar: React.FC<ProgressBarProps> = (props) => {
  const percentage = props.value * 100;
  return (
    <Box
      sx={{
        width: "100%",
        height: "10px",
        backgroundColor: "#78787857",
        borderRadius: "10px",
        margin: "10px 0",
      }}
    >
      <Box
        role="progressbar"
        sx={{
          width: `${percentage}%`,
          height: "10px",
          backgroundColor: props.color ?? ProgressBarColor.Neutral,
          borderRadius: "10px",
        }}
        aria-valuenow={percentage}
        aria-valuemin={0}
        aria-valuemax={100}
      ></Box>
    </Box>
  );
};

export default ProgressBar;
