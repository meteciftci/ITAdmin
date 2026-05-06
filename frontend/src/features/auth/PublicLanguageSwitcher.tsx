import { useMemo } from "react";
import { useTranslation } from "react-i18next";

import { i18n, normalizeLanguage, type SupportedLanguage } from "@/app/i18n";
import { Select } from "@/components/ui/select";

type LanguageOption = {
  value: SupportedLanguage;
  shortLabel: string;
};

export function PublicLanguageSwitcher() {
  const { t } = useTranslation(["common"]);

  const options = useMemo<LanguageOption[]>(
    () => [
      { value: "tr", shortLabel: "TR" },
      { value: "en", shortLabel: "EN" },
    ],
    [],
  );

  const currentLanguage = normalizeLanguage(i18n.language);

  const handleChange = async (next: SupportedLanguage) => {
    if (next === currentLanguage) return;
    await i18n.changeLanguage(next);
  };

  return (
    <div className="flex items-center justify-end gap-2">
      <span className="text-xs text-muted-foreground">{t("language.label")}</span>
      <Select
        className="w-[100px]"
        value={currentLanguage}
        onChange={(event) => void handleChange(event.target.value as SupportedLanguage)}
        aria-label={t("language.label")}
      >
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.shortLabel}
          </option>
        ))}
      </Select>
    </div>
  );
}

