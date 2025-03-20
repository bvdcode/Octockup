import {
  DataGrid,
  GridColDef,
  GridFilterModel,
  GridLocaleText,
  GridSortModel,
  GridToolbar,
  GridValidRowModel,
} from "@mui/x-data-grid";
import { useTranslation } from "react-i18next";
import { forwardRef, useImperativeHandle } from "react";
import { useCallback, useEffect, useState } from "react";
import { Paper, SxProps, Theme, Typography } from "@mui/material";
interface LoadRowsResult<T> {
  data: T[];
  totalCount: number;
}

interface CustomDataGridProps<T extends GridValidRowModel> {
  sx?: SxProps<Theme>;
  title?: string;
  titleAlign?: "left" | "center" | "right";
  showToolbar?: boolean;
  columns: GridColDef<T>[];
  loadRows?: (
    page: number,
    pageSize: number,
    order: GridSortModel | null,
    filter: GridFilterModel | null
  ) => Promise<LoadRowsResult<T>>;
}

export interface CustomDataGridRef {
  reloadData: () => void;
}

const CustomDataGrid = forwardRef<
  CustomDataGridRef,
  CustomDataGridProps<GridValidRowModel>
>(({ sx, title, columns, loadRows, titleAlign, showToolbar }, ref) => {
  const defaultPageSize = 10;
  const { t } = useTranslation();
  const [page, setPage] = useState(0);
  const [loading, setLoading] = useState(false);
  const [totalCount, setTotalCount] = useState(0);
  const [rows, setRows] = useState<GridValidRowModel[]>([]);
  const [pageSize, setPageSize] = useState(defaultPageSize);
  const [order, setOrder] = useState<GridSortModel | null>(null);
  const [filter, setFilter] = useState<GridFilterModel | null>(null);

  const fetchData = useCallback(async () => {
    if (loadRows) {
      setLoading(true);
      const data = await loadRows(page + 1, pageSize, order, filter);
      setRows(data.data);
      setTotalCount(data.totalCount);
      setLoading(false);
    }
  }, [loadRows, page, pageSize, order, filter]);

  useEffect(() => {
    fetchData();
  }, [page, pageSize, filter, order, columns, loadRows, fetchData]);

  useImperativeHandle(ref, () => ({
    reloadData: () => {
      fetchData();
    },
  }));

  const localeText: Partial<GridLocaleText> = {
    MuiTablePagination: {
      labelRowsPerPage: t("DataGrid.RowsPerPage"),
      labelDisplayedRows: ({ from, to, count }) =>
        t("DataGrid.LabelDisplayedRows", {
          from,
          to,
          count: count === -1 ? to : count,
        }),
    },

    columnMenuLabel: t("DataGrid.ColumnMenu"),
    columnMenuShowColumns: t("DataGrid.ShowColumns"),
    columnMenuFilter: t("DataGrid.Filter"),
    columnMenuHideColumn: t("DataGrid.HideColumn"),
    columnMenuManageColumns: t("DataGrid.ManageColumns"),
    columnMenuUnsort: t("DataGrid.Unsort"),
    columnMenuSortAsc: t("DataGrid.SortAsc"),
    columnMenuSortDesc: t("DataGrid.SortDesc"),

    columnsManagementSearchTitle: t("DataGrid.SearchTitle"),
    columnsManagementDeleteIconLabel: t("DataGrid.Delete"),
    columnsManagementNoColumns: t("DataGrid.NoColumns"),
    columnsManagementReset: t("DataGrid.Reset"),
    columnsManagementShowHideAllText: t("DataGrid.ShowHideAll"),

    filterPanelAddFilter: t("DataGrid.AddFilter"),
    filterPanelDeleteIconLabel: t("DataGrid.Delete"),

    noRowsLabel: t("DataGrid.NoRows"),
    noResultsOverlayLabel: t("DataGrid.NoResults"),

    toolbarColumns: t("DataGrid.Columns"),
    toolbarFilters: t("DataGrid.Filters"),
    toolbarDensity: t("DataGrid.Density"),
    toolbarExport: t("DataGrid.Export"),

    toolbarDensityCompact: t("DataGrid.Compact"),
    toolbarDensityStandard: t("DataGrid.Standard"),
    toolbarDensityComfortable: t("DataGrid.Comfortable"),

    toolbarExportCSV: t("DataGrid.ExportCSV"),
    toolbarExportPrint: t("DataGrid.ExportPrint"),

    columnHeaderSortIconLabel: t("DataGrid.SortIconLabel"),
    columnHeaderFiltersLabel: t("DataGrid.FiltersLabel"),
    toolbarExportLabel: t("DataGrid.ExportLabel"),
    toolbarColumnsLabel: t("DataGrid.ColumnsLabel"),
    toolbarDensityLabel: t("DataGrid.DensityLabel"),
    toolbarFiltersLabel: t("DataGrid.FiltersLabel"),
    booleanCellTrueLabel: t("DataGrid.BooleanTrue"),
    booleanCellFalseLabel: t("DataGrid.BooleanFalse"),
    filterPanelInputLabel: t("DataGrid.FilterInputLabel"),
    toolbarQuickFilterLabel: t("DataGrid.QuickFilterLabel"),
    aggregationFunctionLabelAvg: t("DataGrid.Avg"),
    aggregationFunctionLabelMax: t("DataGrid.Max"),
    aggregationFunctionLabelMin: t("DataGrid.Min"),
    aggregationFunctionLabelSize: t("DataGrid.Size"),
    aggregationFunctionLabelSum: t("DataGrid.Sum"),
    toolbarQuickFilterDeleteIconLabel: t("DataGrid.DeleteQuickFilter"),
    toolbarQuickFilterPlaceholder: t("DataGrid.QuickFilterPlaceholder"),
    filterPanelOperatorAnd: t("DataGrid.OperatorAnd"),
    filterPanelOperatorOr: t("DataGrid.OperatorOr"),
    filterPanelColumns: t("DataGrid.FilterColumns"),
    filterOperatorAfter: t("DataGrid.FilterOperatorAfter"),
    filterOperatorBefore: t("DataGrid.FilterOperatorBefore"),
    filterOperatorContains: t("DataGrid.FilterOperatorContains"),
    filterOperatorEquals: t("DataGrid.FilterOperatorEquals"),
    filterOperatorStartsWith: t("DataGrid.FilterOperatorStartsWith"),
    filterOperatorEndsWith: t("DataGrid.FilterOperatorEndsWith"),
    filterOperatorIsEmpty: t("DataGrid.FilterOperatorIsEmpty"),
    filterOperatorIsNotEmpty: t("DataGrid.FilterOperatorIsNotEmpty"),
    filterOperatorIsAnyOf: t("DataGrid.FilterOperatorIsAnyOf"),
    filterOperatorDoesNotContain: t("DataGrid.FilterOperatorDoesNotContain"),
    filterOperatorDoesNotEqual: t("DataGrid.FilterOperatorDoesNotEqual"),
    filterOperatorIs: t("DataGrid.FilterOperatorIs"),
    filterOperatorNot: t("DataGrid.FilterOperatorNot"),
    filterOperatorOnOrAfter: t("DataGrid.FilterOperatorOnOrAfter"),
    filterOperatorOnOrBefore: t("DataGrid.FilterOperatorOnOrBefore"),
    filterPanelInputPlaceholder: t("DataGrid.FilterInputPlaceholder"),
    filterPanelLogicOperator: t("DataGrid.LogicOperator"),
    filterPanelOperator: t("DataGrid.FilterOperator"),
    filterPanelRemoveAll: t("DataGrid.RemoveAllFilters"),
    filterValueAny: t("DataGrid.AnyValue"),
    filterValueTrue: t("DataGrid.True"),
    filterValueFalse: t("DataGrid.False"),
    headerFilterOperatorAfter: t("DataGrid.HeaderFilterOperatorAfter"),
    headerFilterOperatorBefore: t("DataGrid.HeaderFilterOperatorBefore"),
    headerFilterOperatorContains: t("DataGrid.HeaderFilterOperatorContains"),
    headerFilterOperatorEquals: t("DataGrid.HeaderFilterOperatorEquals"),
    headerFilterOperatorStartsWith: t(
      "DataGrid.HeaderFilterOperatorStartsWith"
    ),
    headerFilterOperatorEndsWith: t("DataGrid.HeaderFilterOperatorEndsWith"),
    headerFilterOperatorIsEmpty: t("DataGrid.HeaderFilterOperatorIsEmpty"),
    headerFilterOperatorIsNotEmpty: t(
      "DataGrid.HeaderFilterOperatorIsNotEmpty"
    ),
    headerFilterOperatorIsAnyOf: t("DataGrid.HeaderFilterOperatorIsAnyOf"),
    headerFilterOperatorDoesNotContain: t(
      "DataGrid.HeaderFilterOperatorDoesNotContain"
    ),
    headerFilterOperatorDoesNotEqual: t(
      "DataGrid.HeaderFilterOperatorDoesNotEqual"
    ),
    headerFilterOperatorIs: t("DataGrid.HeaderFilterOperatorIs"),
    headerFilterOperatorNot: t("DataGrid.HeaderFilterOperatorNot"),
    headerFilterOperatorOnOrAfter: t("DataGrid.HeaderFilterOperatorOnOrAfter"),
    headerFilterOperatorOnOrBefore: t(
      "DataGrid.HeaderFilterOperatorOnOrBefore"
    ),
    collapseDetailPanel: t("DataGrid.CollapseDetailPanel"),
    columnHeaderFiltersTooltipActive: (count) =>
      t("DataGrid.FiltersActive", { count }),
    groupColumn: (column) => t("DataGrid.GroupColumn", { column }),
    unGroupColumn: (column) => t("DataGrid.UnGroupColumn", { column }),
    detailPanelToggle: t("DataGrid.DetailPanelToggle"),
  };

  return (
    <Paper
      sx={{
        borderRadius: 0,
        overflowX: "auto",
        width: "100%",
        maxWidth: "100vw",
        display: "flex",
        flexDirection: "column",
        ...sx,
      }}
    >
      {title && (
        <Typography
          variant="h6"
          sx={{
            padding: 2,
            width: "100%",
            textAlign: titleAlign || "center",
          }}
        >
          {title}
        </Typography>
      )}
      <DataGrid
        rows={rows}
        columns={columns}
        rowCount={totalCount}
        loading={loading}
        pagination={true}
        localeText={localeText}
        sortingMode="server"
        paginationMode="server"
        onPaginationModelChange={(model) => {
          setPage(model.page);
          setPageSize(model.pageSize);
        }}
        onFilterModelChange={(model) => {
          setFilter(model);
        }}
        onSortModelChange={(model) => {
          setOrder(model);
        }}
        initialState={{
          pagination: {
            paginationModel: {
              pageSize: defaultPageSize,
            },
          },
        }}
        checkboxSelection={false}
        pageSizeOptions={[5, defaultPageSize, 25, 50, 100]}
        disableRowSelectionOnClick={true}
        slots={{
          toolbar: showToolbar ? GridToolbar : undefined,
        }}
        density="compact"
      />
    </Paper>
  );
});

export default CustomDataGrid;
