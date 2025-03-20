import { Box } from "@mui/material";
import { ArrowDownward, ArrowForward } from "@mui/icons-material";

const DirectionArrow: React.FC = () => {
  return (
    <Box>
      <ArrowForward
        fontSize="large"
        sx={{
          display: {
            xs: "none",
            md: "block",
          },
        }}
      />
      <ArrowDownward
        fontSize="large"
        sx={{
          display: {
            xs: "block",
            md: "none",
          },
        }}
      />
    </Box>
  );
};

export default DirectionArrow;
