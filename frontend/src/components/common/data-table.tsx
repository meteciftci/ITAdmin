import type { ReactNode } from "react";
import { useState } from "react";
import { flexRender, type Table as TanStackTable } from "@tanstack/react-table";
import { Inbox, LoaderCircle, Search, SlidersHorizontal, X } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Select } from "@/components/ui/select";
import { cn } from "@/lib/utils";

export type DataTableColumnAlign = "left" | "center" | "right";

export type DataTableColumnMeta = {
  isAction?: boolean;
  align?: DataTableColumnAlign;
  truncate?: boolean;
  mono?: boolean;
  headerClassName?: string;
  cellClassName?: string;
};

export type DataTableFilterItem = {
  id: string;
  label: string;
  value: string;
  onRemove: () => void;
};

export type DataTablePaginationState = {
  pageIndex: number;
  pageSize: number;
};

export type DataTableServerPaginationProps = {
  mode: "server";
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (pageNumber: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: number[];
  summaryText?: string;
  showPageSize?: boolean;
  showSummary?: boolean;
};

export type DataTableDirectoryPaginationProps = {
  mode: "directory";
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
  onPageChange: (pageNumber: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: number[];
  summaryText?: string;
};

export type DataTableClientPaginationProps<TData> = {
  mode: "client";
  table: TanStackTable<TData>;
  pageSizeOptions?: number[];
  showPageSize?: boolean;
  showSummary?: boolean;
  summaryText?: string;
};

export type DataTablePaginationProps<TData> =
  | DataTableServerPaginationProps
  | DataTableDirectoryPaginationProps
  | DataTableClientPaginationProps<TData>;

const DEFAULT_PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

const ACTION_COLUMN_WIDTH_CLASS = "w-0 whitespace-nowrap overflow-visible";

function getEffectiveAlign(
  meta: DataTableColumnMeta | undefined,
): DataTableColumnAlign | undefined {
  if (meta?.align) {
    return meta.align;
  }

  if (meta?.isAction) {
    return "right";
  }

  return undefined;
}

function getAlignClassName(align: DataTableColumnAlign | undefined) {
  if (align === "center") {
    return "text-center";
  }

  if (align === "right") {
    return "text-right";
  }

  if (align === "left") {
    return "text-left";
  }

  return undefined;
}

function getHeaderClassName(meta: DataTableColumnMeta | undefined) {
  return cn(
    "h-11 px-4 text-xs font-semibold uppercase tracking-[0.06em] text-muted-foreground",
    meta?.isAction ? ACTION_COLUMN_WIDTH_CLASS : "whitespace-nowrap",
    meta?.isAction &&
      "sticky right-0 z-10 bg-muted/95 shadow-[-10px_0_14px_-14px_rgb(0_0_0/0.45)]",
    getAlignClassName(getEffectiveAlign(meta)),
    meta?.headerClassName,
  );
}

function getCellClassName(meta: DataTableColumnMeta | undefined) {
  return cn(
    "px-4 py-3 align-middle",
    meta?.isAction && ACTION_COLUMN_WIDTH_CLASS,
    meta?.isAction &&
      "sticky right-0 z-[1] bg-card shadow-[-10px_0_14px_-14px_rgb(0_0_0/0.45)] group-hover:bg-muted",
    getAlignClassName(getEffectiveAlign(meta)),
    meta?.truncate && "max-w-48 truncate",
    meta?.mono && "font-mono text-xs text-muted-foreground",
    meta?.cellClassName,
  );
}

function getCellContentClassName(meta: DataTableColumnMeta | undefined) {
  const align = getEffectiveAlign(meta);

  if (!align || align === "left") {
    return meta?.isAction ? "flex justify-start" : null;
  }

  if (align === "center") {
    return "flex justify-center";
  }

  return "flex justify-end";
}

type DataTableProps<TData> = {
  table: TanStackTable<TData>;
  scrollable?: boolean;
  emptyMessage?: ReactNode;
  emptyDescription?: ReactNode;
  isLoading?: boolean;
  loadingMessage?: ReactNode;
  footer?: ReactNode;
};

export function DataTable<TData>({
  table,
  scrollable = true,
  emptyMessage,
  emptyDescription,
  isLoading = false,
  loadingMessage,
  footer,
}: DataTableProps<TData>) {
  const { t } = useTranslation("common");
  const rows = table.getRowModel().rows;
  const resolvedEmptyMessage = emptyMessage ?? t("dataTable.noResults");
  const resolvedLoadingMessage = loadingMessage ?? t("dataTable.loading");
  const columnCount = table.getVisibleLeafColumns().length;

  return (
    <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
      <div className={cn(scrollable && "overflow-x-auto")}>
        <table className="min-w-full text-sm">
          <thead className="border-b bg-muted/55 text-left">
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id}>
                {headerGroup.headers.map((header) => {
                  const meta = header.column.columnDef.meta as DataTableColumnMeta | undefined;
                  return (
                    <th key={header.id} scope="col" className={getHeaderClassName(meta)}>
                      {header.isPlaceholder
                        ? null
                        : flexRender(header.column.columnDef.header, header.getContext())}
                    </th>
                  );
                })}
              </tr>
            ))}
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td
                  colSpan={columnCount}
                  className="h-36 px-4 text-center text-sm text-muted-foreground"
                >
                  <div className="flex flex-col items-center justify-center gap-3" role="status">
                    <LoaderCircle className="size-5 animate-spin text-primary" aria-hidden />
                    <span>{resolvedLoadingMessage}</span>
                  </div>
                </td>
              </tr>
            ) : null}
            {!isLoading && rows.length === 0 ? (
              <tr>
                <td
                  colSpan={columnCount}
                  className="h-36 px-4 text-center text-sm text-muted-foreground"
                >
                  <div className="flex flex-col items-center justify-center gap-2 py-5">
                    <span className="mb-1 flex size-10 items-center justify-center rounded-full bg-muted text-muted-foreground">
                      <Inbox className="size-5" aria-hidden />
                    </span>
                    <span className="font-medium text-foreground">{resolvedEmptyMessage}</span>
                    {emptyDescription ? (
                      <span className="max-w-md leading-6">{emptyDescription}</span>
                    ) : null}
                  </div>
                </td>
              </tr>
            ) : null}
            {!isLoading
              ? rows.map((row) => (
                  <tr key={row.id} className="group border-t transition-colors hover:bg-muted/35">
                    {row.getVisibleCells().map((cell) => {
                      const meta = cell.column.columnDef.meta as DataTableColumnMeta | undefined;
                      const title =
                        meta?.truncate && typeof cell.getValue() === "string"
                          ? (cell.getValue() as string)
                          : undefined;
                      const contentWrapperClass = getCellContentClassName(meta);
                      const cellContent = flexRender(
                        cell.column.columnDef.cell,
                        cell.getContext(),
                      );

                      return (
                        <td key={cell.id} className={getCellClassName(meta)} title={title}>
                          {contentWrapperClass ? (
                            <div className={contentWrapperClass}>{cellContent}</div>
                          ) : (
                            cellContent
                          )}
                        </td>
                      );
                    })}
                  </tr>
                ))
              : null}
          </tbody>
        </table>
      </div>
      {footer}
    </div>
  );
}

type DataTableToolbarProps = {
  searchValue?: string;
  onSearchChange?: (value: string) => void;
  searchPlaceholder?: string;
  actions?: ReactNode;
  filterContent?: ReactNode;
  activeFilterCount?: number;
  onClearFilters?: () => void;
  showFiltersButton?: boolean;
  activeFilters?: DataTableFilterItem[];
  toolbarFooter?: ReactNode;
};

export function DataTableToolbar({
  searchValue,
  onSearchChange,
  searchPlaceholder,
  actions,
  filterContent,
  activeFilterCount = 0,
  onClearFilters,
  showFiltersButton = true,
  activeFilters,
  toolbarFooter,
}: DataTableToolbarProps) {
  const { t } = useTranslation("common");
  const [filtersOpen, setFiltersOpen] = useState(false);
  const hasFilters = Boolean(filterContent);
  const showFilterButton = hasFilters && showFiltersButton;

  return (
    <div className="space-y-3">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex min-w-0 flex-1 flex-col gap-3 sm:flex-row sm:items-center">
          {onSearchChange ? (
            <div className="relative min-w-0 flex-1 sm:min-w-[240px]">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                value={searchValue ?? ""}
                onChange={(event) => onSearchChange(event.target.value)}
                placeholder={searchPlaceholder ?? t("dataTable.search")}
                className="w-full pl-9"
                aria-label={t("dataTable.search")}
              />
            </div>
          ) : null}
        </div>
        <div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
          {showFilterButton ? (
            <DataTableFilterPanel
              open={filtersOpen}
              onOpenChange={setFiltersOpen}
              activeFilterCount={activeFilterCount}
              onClearFilters={onClearFilters}
            >
              {filterContent}
            </DataTableFilterPanel>
          ) : null}
          {actions}
        </div>
      </div>

      {activeFilters && activeFilters.length > 0 ? (
        <DataTableActiveFilters
          filters={activeFilters}
          onClearAll={onClearFilters}
        />
      ) : null}

      {toolbarFooter}
    </div>
  );
}

type DataTableFilterPanelProps = {
  children: ReactNode;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  activeFilterCount?: number;
  onClearFilters?: () => void;
};

export function DataTableFilterPanel({
  children,
  open,
  onOpenChange,
  activeFilterCount = 0,
  onClearFilters,
}: DataTableFilterPanelProps) {
  const { t } = useTranslation("common");

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger asChild>
        <Button type="button" variant="outline" className="gap-2">
          <SlidersHorizontal className="size-4" />
          {t("dataTable.filters")}
          {activeFilterCount > 0 ? (
            <Badge variant="secondary" className="px-1.5 py-0 text-xs">
              {activeFilterCount}
            </Badge>
          ) : null}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-[min(20rem,calc(100vw-2rem))] space-y-4 p-4">
        <div className="space-y-3">{children}</div>
        <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3">
          {onClearFilters ? (
            <Button type="button" variant="ghost" size="sm" onClick={onClearFilters}>
              {t("dataTable.clearFilters")}
            </Button>
          ) : (
            <span className="text-xs text-muted-foreground">{t("dataTable.noFilters")}</span>
          )}
          <Button type="button" variant="outline" size="sm" onClick={() => onOpenChange(false)}>
            {t("actions.close")}
          </Button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

type DataTableActiveFiltersProps = {
  filters: DataTableFilterItem[];
  onClearAll?: () => void;
};

export function DataTableActiveFilters({ filters, onClearAll }: DataTableActiveFiltersProps) {
  const { t } = useTranslation("common");

  if (filters.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="text-xs font-medium text-muted-foreground">
        {t("dataTable.activeFilters")}:
      </span>
      {filters.map((filter) => (
        <Badge key={filter.id} variant="outline" className="gap-1 pr-1">
          <span className="text-xs">
            {filter.label}: {filter.value}
          </span>
          <button
            type="button"
            className="rounded-sm p-0.5 hover:bg-muted"
            onClick={filter.onRemove}
            aria-label={t("dataTable.removeFilter", { label: filter.label })}
          >
            <X className="size-3" />
          </button>
        </Badge>
      ))}
      {onClearAll ? (
        <Button type="button" variant="ghost" size="sm" onClick={onClearAll}>
          {t("dataTable.clearFilters")}
        </Button>
      ) : null}
    </div>
  );
}

export function DataTablePagination<TData>(props: DataTablePaginationProps<TData>) {
  const { t } = useTranslation("common");

  if (props.mode === "directory") {
    const pageSizeOptions = props.pageSizeOptions ?? DEFAULT_PAGE_SIZE_OPTIONS;
    const isFirstPage = props.pageNumber <= 1;
    const summary =
      props.summaryText ?? t("pagination.pageOnly", { pageNumber: props.pageNumber });
    return (
      <div className="flex flex-col gap-4 border-t bg-muted/20 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-muted-foreground">{summary}</p>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center gap-2">
            <label
              className="text-sm text-muted-foreground"
              htmlFor="data-table-directory-page-size"
            >
              {t("pagination.pageSize")}
            </label>
            <Select
              id="data-table-directory-page-size"
              value={String(props.pageSize)}
              onChange={(event) => props.onPageSizeChange(Number(event.target.value))}
              className="w-20"
            >
              {pageSizeOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </Select>
          </div>
          <Button
            variant="outline"
            size="sm"
            disabled={isFirstPage}
            onClick={() => props.onPageChange(1)}
          >
            {t("pagination.first")}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={isFirstPage}
            onClick={() => props.onPageChange(props.pageNumber - 1)}
          >
            {t("pagination.previous")}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!props.hasNextPage}
            onClick={() => props.onPageChange(props.pageNumber + 1)}
          >
            {t("pagination.next")}
          </Button>
        </div>
      </div>
    );
  }

  if (props.mode === "client") {
    const table = props.table;
    const pageSizeOptions = props.pageSizeOptions ?? DEFAULT_PAGE_SIZE_OPTIONS;
    const pageIndex = table.getState().pagination.pageIndex;
    const pageSize = table.getState().pagination.pageSize;
    const pageCount = Math.max(table.getPageCount(), 1);
    const pageNumber = pageIndex + 1;
    const totalCount = table.getFilteredRowModel().rows.length;
    const isFirstPage = !table.getCanPreviousPage();
    const isLastPage = !table.getCanNextPage();
    const showPageSize = props.showPageSize ?? true;
    const showSummary = props.showSummary ?? true;

    return (
      <div className="flex flex-col gap-4 border-t bg-muted/20 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
        {showSummary ? (
          <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
            {props.summaryText ? (
              <span>{props.summaryText}</span>
            ) : (
              <>
                <span>{t("pagination.totalCount", { count: totalCount })}</span>
                <span>
                  {t("pagination.pageInfo", {
                    pageNumber,
                    totalPages: pageCount,
                  })}
                </span>
              </>
            )}
          </div>
        ) : (
          <div />
        )}
        <div className="flex flex-wrap items-center gap-2">
          {showPageSize ? (
            <div className="flex items-center gap-2">
              <label
                className="text-sm text-muted-foreground"
                htmlFor="data-table-client-page-size"
              >
                {t("pagination.pageSize")}
              </label>
              <Select
                id="data-table-client-page-size"
                value={String(pageSize)}
                onChange={(event) => table.setPageSize(Number(event.target.value))}
                className="w-20"
              >
                {pageSizeOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </Select>
            </div>
          ) : null}
          <Button
            variant="outline"
            size="sm"
            disabled={isFirstPage}
            onClick={() => table.setPageIndex(0)}
          >
            {t("pagination.first")}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={isFirstPage}
            onClick={() => table.previousPage()}
          >
            {t("pagination.previous")}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={isLastPage}
            onClick={() => table.nextPage()}
          >
            {t("pagination.next")}
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={isLastPage}
            onClick={() => table.setPageIndex(pageCount - 1)}
          >
            {t("pagination.last")}
          </Button>
        </div>
      </div>
    );
  }

  const safeTotalPages = Math.max(props.totalPages, 1);
  const safePageNumber = Math.min(Math.max(props.pageNumber, 1), safeTotalPages);
  const isFirstPage = safePageNumber <= 1;
  const isLastPage = safePageNumber >= safeTotalPages;
  const pageSizeOptions = props.pageSizeOptions ?? DEFAULT_PAGE_SIZE_OPTIONS;
  const showPageSize = props.showPageSize ?? true;
  const showSummary = props.showSummary ?? true;

  return (
    <div className="flex flex-col gap-4 border-t bg-muted/20 px-4 py-4 sm:flex-row sm:items-center sm:justify-between">
      {showSummary ? (
        <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
          {props.summaryText ? (
            <span>{props.summaryText}</span>
          ) : (
            <>
              <span>{t("pagination.totalCount", { count: props.totalCount })}</span>
              <span>
                {t("pagination.pageInfo", {
                  pageNumber: safePageNumber,
                  totalPages: safeTotalPages,
                })}
              </span>
            </>
          )}
        </div>
      ) : (
        <div />
      )}

      <div className="flex flex-wrap items-center gap-2">
        {showPageSize ? (
          <div className="flex items-center gap-2">
            <label
              className="text-sm text-muted-foreground"
              htmlFor="data-table-server-page-size"
            >
              {t("pagination.pageSize")}
            </label>
            <Select
              id="data-table-server-page-size"
              value={String(props.pageSize)}
              onChange={(event) => props.onPageSizeChange(Number(event.target.value))}
              className="w-20"
            >
              {pageSizeOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </Select>
          </div>
        ) : null}

        <Button variant="outline" size="sm" disabled={isFirstPage} onClick={() => props.onPageChange(1)}>
          {t("pagination.first")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={isFirstPage}
          onClick={() => props.onPageChange(safePageNumber - 1)}
        >
          {t("pagination.previous")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={isLastPage}
          onClick={() => props.onPageChange(safePageNumber + 1)}
        >
          {t("pagination.next")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={isLastPage}
          onClick={() => props.onPageChange(safeTotalPages)}
        >
          {t("pagination.last")}
        </Button>
      </div>
    </div>
  );
}
