import { useEffect, useState } from "react";
import { BackupProvider } from "../api/types";
import { getProviders } from "../api/api";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Box,
  Typography,
  Button,
  TextField,
  MenuItem,
  FormControl,
  InputLabel,
  Select,
  Paper,
  Card,
  CardContent,
  CardActions,
  Stack,
} from "@mui/material";

const CreateJob: React.FC = () => {
  const [providers, setProviders] = useState<BackupProvider[]>([]);
  useEffect(() => {
    getProviders().then((response) => {
      setProviders(response);
    });
  }, []);

  return (
    <>
      <Card>
        <CardContent>
          <Typography variant="h6">Выбор провайдера</Typography>
          <FormControl fullWidth margin="normal">
            <InputLabel id="provider-select-label">Провайдер</InputLabel>
            <Select
              labelId="provider-select-label"
              id="provider-select"
              label="Провайдер"
            >
              {providers.map((provider) => (
                <MenuItem key={provider.id} value={provider.id}>
                  {provider.name}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Параметр</TableCell>
                <TableCell>Значение</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {/* Example data, replace with actual provider parameters */}
              <TableRow>
                <TableCell>Пример параметра</TableCell>
                <TableCell>Пример значения</TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6">Блок параметров</Typography>
          <TextField
            fullWidth
            margin="normal"
            label="Параметр 1"
            variant="outlined"
          />
          <TextField
            fullWidth
            margin="normal"
            label="Параметр 2"
            variant="outlined"
          />
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="h6">Настройки</Typography>
          <TextField
            fullWidth
            margin="normal"
            label="Частота бэкапов"
            variant="outlined"
          />
          <TextField
            fullWidth
            margin="normal"
            label="Когда начинать"
            variant="outlined"
          />
          <FormControl fullWidth margin="normal">
            <InputLabel id="notifications-select-label">Уведомления</InputLabel>
            <Select
              labelId="notifications-select-label"
              id="notifications-select"
              label="Уведомления"
            >
              <MenuItem value="yes">Да</MenuItem>
              <MenuItem value="no">Нет</MenuItem>
            </Select>
          </FormControl>
        </CardContent>
      </Card>
    </>
  );
};

export default CreateJob;
