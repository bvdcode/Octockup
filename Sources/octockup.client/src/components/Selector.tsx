import {
  Box,
  FormControl,
  Icon,
  InputLabel,
  MenuItem,
  Select,
  Typography,
} from "@mui/material";

interface SelectorOption {
  id: number;
  name: string;
  icon: React.ReactNode;
}

interface SelectorProps {
  title: string;
  values: SelectorOption[];
  onSelected?: (value: SelectorOption) => void;
}

const Selector: React.FC<SelectorProps> = ({ title, values, onSelected }) => {
  return (
    <FormControl fullWidth>
      <InputLabel id="select-label">
        {title}
      </InputLabel>
      <Select
        labelId="select-label"
        sx={{
          width: "100%",
          marginTop: 1,
        }}
        variant="outlined"
        value={undefined}
        onChange={(event) => {
          if (!event.target.value) {
            return;
          }
          const selectedValue = values.find(
            (value) => value.id === Number(event.target.value)
          );
          if (selectedValue && onSelected) {
            onSelected(selectedValue);
          }
        }}
      >
        {values.map((source) => (
          <MenuItem
            key={source.id}
            value={source.id}
            sx={{
              display: "flex",
              flexDirection: "row",
              alignItems: "center",
              justifyContent: "flex-start",
              gap: 1,
            }}
          >
            <Box
              display="flex"
              flexDirection="row"
              alignItems="center"
              justifyContent="flex-start"
              gap={1}
            >
              <Icon>{source.icon}</Icon>
              <Typography>{source.name}</Typography>
            </Box>
          </MenuItem>
        ))}
      </Select>
    </FormControl>
  );
};

export default Selector;
