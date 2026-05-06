import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";

type TablePaginationProps = {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  onPageChange: (pageNumber: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: number[];
};

const DEFAULT_PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

export function TablePagination({
  pageNumber,
  pageSize,
  totalCount,
  totalPages,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = DEFAULT_PAGE_SIZE_OPTIONS,
}: TablePaginationProps) {
  const { t } = useTranslation("common");

  const safeTotalPages = Math.max(totalPages, 1);
  const safePageNumber = Math.min(Math.max(pageNumber, 1), safeTotalPages);
  const isFirstPage = safePageNumber <= 1;
  const isLastPage = safePageNumber >= safeTotalPages;

  return (
    <div className="flex flex-col gap-3 border-t px-3 py-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex flex-wrap items-center gap-3 text-sm text-muted-foreground">
        <span>{t("pagination.totalCount", { count: totalCount })}</span>
        <span>{t("pagination.pageInfo", { pageNumber: safePageNumber, totalPages: safeTotalPages })}</span>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <div className="flex items-center gap-2">
          <label className="text-sm text-muted-foreground" htmlFor="table-pagination-page-size">
            {t("pagination.pageSize")}
          </label>
          <Select
            id="table-pagination-page-size"
            value={String(pageSize)}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
            className="w-20"
          >
            {pageSizeOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </Select>
        </div>

        <Button variant="outline" size="sm" disabled={isFirstPage} onClick={() => onPageChange(1)}>
          {t("pagination.first")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={isFirstPage}
          onClick={() => onPageChange(safePageNumber - 1)}
        >
          {t("pagination.previous")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={isLastPage}
          onClick={() => onPageChange(safePageNumber + 1)}
        >
          {t("pagination.next")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={isLastPage}
          onClick={() => onPageChange(safeTotalPages)}
        >
          {t("pagination.last")}
        </Button>
      </div>
    </div>
  );
}
