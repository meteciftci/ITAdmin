import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { normalizeLanguage } from "@/app/i18n";

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
  const {
    t,
    i18n: translationI18n,
  } = useTranslation(["common"]);

  const display = useMemo(() => {
    if (!value) return emptyText ?? "-";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return emptyText ?? "-";

    const locale = getLocale(translationI18n.resolvedLanguage ?? translationI18n.language);
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
  }, [value, emptyText, options, translationI18n.language, translationI18n.resolvedLanguage]);

  const hasValidDate = Boolean(value) && !Number.isNaN(new Date(value ?? "").getTime());

  return (
    <span aria-label={hasValidDate ? undefined : (emptyText ?? t("notAvailable"))}>
      {display}
    </span>
  );
}

