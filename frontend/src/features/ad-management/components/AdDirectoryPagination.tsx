import { Button } from "@/components/ui/button";
import { Select } from "@/components/ui/select";
import { useTranslation } from "react-i18next";

type Props = {
  pageNumber: number;
  pageSize: number;
  hasNextPage: boolean;
  onPageChange: (pageNumber: number) => void;
  onPageSizeChange: (pageSize: number) => void;
};

const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

export function AdDirectoryPagination({
  pageNumber,
  pageSize,
  hasNextPage,
  onPageChange,
  onPageSizeChange,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);

  return (
    <div className="flex flex-col gap-3 border-t px-3 py-3 sm:flex-row sm:items-center sm:justify-between">
      <p className="text-sm text-muted-foreground">
        {t("adManagement:users.pagination.page", { pageNumber })}
      </p>
      <div className="flex flex-wrap items-center gap-2">
        <Select
          value={String(pageSize)}
          onChange={(event) => onPageSizeChange(Number.parseInt(event.target.value, 10))}
          className="w-24"
          aria-label={t("common:pagination.pageSize")}
        >
          {PAGE_SIZE_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </Select>
        <Button
          variant="outline"
          size="sm"
          disabled={pageNumber <= 1}
          onClick={() => onPageChange(pageNumber - 1)}
        >
          {t("adManagement:users.pagination.previous")}
        </Button>
        <Button
          variant="outline"
          size="sm"
          disabled={!hasNextPage}
          onClick={() => onPageChange(pageNumber + 1)}
        >
          {t("adManagement:users.pagination.next")}
        </Button>
      </div>
    </div>
  );
}
