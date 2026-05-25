import { useMemo, useState } from "react";
import {
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  useReactTable,
  type ColumnDef,
  type PaginationState,
  type TableOptions,
} from "@tanstack/react-table";

export function useServerDataTable<TData>({
  data,
  columns,
  pageCount,
  pageIndex,
  pageSize,
}: {
  data: TData[];
  columns: ColumnDef<TData, unknown>[];
  pageCount: number;
  pageIndex: number;
  pageSize: number;
}) {
  return useReactTable({
    data,
    columns,
    pageCount,
    state: {
      pagination: {
        pageIndex,
        pageSize,
      },
    },
    manualPagination: true,
    getCoreRowModel: getCoreRowModel(),
  });
}

type UseClientDataTableOptions<TData> = {
  data: TData[];
  columns: ColumnDef<TData, unknown>[];
  initialPageSize?: number;
  globalFilter?: string;
  enableGlobalFilter?: boolean;
  enablePagination?: boolean;
};

export function useClientDataTable<TData>({
  data,
  columns,
  initialPageSize = 10,
  globalFilter = "",
  enableGlobalFilter = false,
  enablePagination = true,
}: UseClientDataTableOptions<TData>) {
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: initialPageSize,
  });
  const [internalGlobalFilter, setInternalGlobalFilter] = useState(globalFilter);

  const tableOptions = useMemo<TableOptions<TData>>(
    () => ({
      data,
      columns,
      state: {
        pagination: enablePagination ? pagination : undefined,
        globalFilter: enableGlobalFilter ? internalGlobalFilter : undefined,
      },
      onPaginationChange: enablePagination ? setPagination : undefined,
      onGlobalFilterChange: enableGlobalFilter ? setInternalGlobalFilter : undefined,
      getCoreRowModel: getCoreRowModel(),
      getFilteredRowModel: enableGlobalFilter ? getFilteredRowModel() : undefined,
      getPaginationRowModel: enablePagination ? getPaginationRowModel() : undefined,
    }),
    [columns, data, enableGlobalFilter, enablePagination, internalGlobalFilter, pagination],
  );

  return useReactTable(tableOptions);
}
