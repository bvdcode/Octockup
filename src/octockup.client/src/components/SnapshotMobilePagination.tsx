import {
  Box,
  FormControl,
  IconButton,
  MenuItem,
  Select,
  Typography,
} from "@mui/material";
import { ChevronLeft, ChevronRight } from "@mui/icons-material";
import { useTranslation } from "react-i18next";

interface SnapshotMobilePaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
  onChange: (page: number, pageSize: number) => void;
}

export default function SnapshotMobilePagination({
  page,
  pageSize,
  totalCount,
  hasNextPage,
  onChange,
}: SnapshotMobilePaginationProps) {
  const { t } = useTranslation();
  const from = totalCount === 0 ? 0 : page * pageSize + 1;
  const to = Math.min(totalCount, (page + 1) * pageSize);

  return (
    <Box
      display="flex"
      alignItems="center"
      justifyContent="space-between"
      gap={1}
    >
      <FormControl size="small">
        <Select
          value={pageSize}
          onChange={(event) => onChange(0, Number(event.target.value))}
          inputProps={{
            "aria-label": t("snapshots.pagination.rowsPerPage"),
          }}
        >
          {[10, 25, 50, 100].map((size) => (
            <MenuItem key={size} value={size}>
              {size}
            </MenuItem>
          ))}
        </Select>
      </FormControl>
      <Typography variant="body2" color="text.secondary">
        {t("snapshots.pagination.displayedRows", { from, to, count: totalCount })}
      </Typography>
      <Box display="flex">
        <IconButton
          aria-label={t("snapshots.pagination.previous")}
          disabled={page === 0}
          onClick={() => onChange(page - 1, pageSize)}
        >
          <ChevronLeft />
        </IconButton>
        <IconButton
          aria-label={t("snapshots.pagination.next")}
          disabled={!hasNextPage}
          onClick={() => onChange(page + 1, pageSize)}
        >
          <ChevronRight />
        </IconButton>
      </Box>
    </Box>
  );
}
