import { useEffect, useMemo, useRef, useState } from "react";
import {
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  useReactTable,
  type ColumnDef,
  type FilterFn,
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
  // eslint-disable-next-line react-hooks/incompatible-library -- TanStack Table's useReactTable intentionally returns non-memoizable table APIs.
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
  /**
   * Builds the searchable text for a row when global filter is enabled.
   * Required to make search work over computed/label cells (where the raw
   * accessor value differs from what the user sees).
   */
  getSearchableValue?: (row: TData) => string;
};

export function useClientDataTable<TData>({
  data,
  columns,
  initialPageSize = 10,
  globalFilter = "",
  enableGlobalFilter = false,
  enablePagination = true,
  getSearchableValue,
}: UseClientDataTableOptions<TData>) {
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: initialPageSize,
  });

  // Reset to the first page whenever the (controlled) search value changes so
  // filtered rows stay consistent with the visible page.
  const previousGlobalFilter = useRef(globalFilter);
  useEffect(() => {
    if (!enableGlobalFilter) {
      return;
    }
    if (previousGlobalFilter.current !== globalFilter) {
      previousGlobalFilter.current = globalFilter;
      setPagination((current) =>
        current.pageIndex === 0 ? current : { ...current, pageIndex: 0 },
      );
    }
  }, [enableGlobalFilter, globalFilter]);

  const globalFilterFn = useMemo<FilterFn<TData> | undefined>(() => {
    if (!enableGlobalFilter || !getSearchableValue) {
      return undefined;
    }
    return (row, _columnId, filterValue) => {
      const search = String(filterValue ?? "").trim().toLocaleLowerCase();
      if (!search) {
        return true;
      }
      return getSearchableValue(row.original).toLocaleLowerCase().includes(search);
    };
  }, [enableGlobalFilter, getSearchableValue]);

  const tableOptions = useMemo<TableOptions<TData>>(
    () => ({
      data,
      columns,
      state: {
        pagination: enablePagination ? pagination : undefined,
        globalFilter: enableGlobalFilter ? globalFilter : undefined,
      },
      onPaginationChange: enablePagination ? setPagination : undefined,
      globalFilterFn,
      getCoreRowModel: getCoreRowModel(),
      getFilteredRowModel: enableGlobalFilter ? getFilteredRowModel() : undefined,
      getPaginationRowModel: enablePagination ? getPaginationRowModel() : undefined,
    }),
    [columns, data, enableGlobalFilter, enablePagination, globalFilter, globalFilterFn, pagination],
  );

  // eslint-disable-next-line react-hooks/incompatible-library -- TanStack Table's useReactTable intentionally returns non-memoizable table APIs.
  return useReactTable(tableOptions);
}
