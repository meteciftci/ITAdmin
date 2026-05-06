import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { i18n, normalizeLanguage } from "@/app/i18n";

type DateTimeTextProps = {
  value?: string | null;
  emptyText?: string;
  options?: Intl.DateTimeFormatOptions;
};

const getLocale = (language: string): string => {
  const normalized = normalizeLanguage(language);
  return normalized === "en" ? "en-US" : "tr-TR";
};

export function DateTimeText({ value, emptyText, options }: DateTimeTextProps) {
  const { t } = useTranslation(["common"]);

  const display = useMemo(() => {
    if (!value) return emptyText ?? "-";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return emptyText ?? "-";

    const locale = getLocale(i18n.language);
    const formatter = new Intl.DateTimeFormat(
      locale,
      options ?? {
        year: "numeric",
        month: "2-digit",
        day: "2-digit",
        hour: "2-digit",
        minute: "2-digit",
      },
    );
    return formatter.format(date);
  }, [value, emptyText, options]);

  const ariaLabel = emptyText ?? t("notAvailable");
  return <span aria-label={ariaLabel}>{display}</span>;
}

