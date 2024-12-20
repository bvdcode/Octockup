import { useEffect, useState } from "react";
import { BackupProvider } from "../api/types";
import { getProviders } from "../api/api";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
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
      <Table sx={{ minWidth: 650 }}>
        <TableHead>
          <TableRow>
            <TableCell>Provider</TableCell>
            <TableCell align="right">Params</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {providers.map((provider, index) => (
            <TableRow key={index}>
              <TableCell component="th" scope="row">
                {provider.name}
              </TableCell>
              <TableCell align="right">
                {provider.parameters.join(", ")}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </>
  );
};

export default CreateJob;
