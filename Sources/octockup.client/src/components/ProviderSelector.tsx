import {
  Box,
  FormControl,
  Icon,
  InputLabel,
  MenuItem,
  Select,
  Typography,
} from "@mui/material";

interface ProviderSelectorOption {
  id: number;
  name: string;
  icon: React.ReactNode;
}

interface ProviderSelectorProps {
  title: string;
  values: ProviderSelectorOption[];
  onSelected?: (value: ProviderSelectorOption) => void;
}

const ProviderSelector: React.FC<ProviderSelectorProps> = ({
  title,
  values,
  onSelected,
}) => {
  return (
    <FormControl fullWidth>
      <InputLabel id="select-label">{title}</InputLabel>
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

export default ProviderSelector;
