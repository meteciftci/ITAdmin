import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";

import { i18n, normalizeLanguage, type SupportedLanguage } from "@/app/i18n";
import { updateCurrentUserPreferences } from "@/features/auth/api";
import { useAuthStore } from "@/features/auth/auth-store";
import { Select } from "@/components/ui/select";

type LanguageOption = {
  value: SupportedLanguage;
  label: string;
  shortLabel: string;
};

export function LanguageSwitcher({ compact = false }: { compact?: boolean }) {
  const { t } = useTranslation(["common"]);
  const user = useAuthStore((state) => state.user);
  const updateUser = useAuthStore((state) => state.updateUser);

  const options = useMemo<LanguageOption[]>(
    () => [
      {
        value: "tr",
        label: t("language.turkish"),
        shortLabel: "TR",
      },
      {
        value: "en",
        label: t("language.english"),
        shortLabel: "EN",
      },
    ],
    [t],
  );

  const currentLanguage = normalizeLanguage(user?.preferredLanguage ?? i18n.language);
  const [isSaving, setIsSaving] = useState(false);

  const onChangeLanguage = async (next: SupportedLanguage) => {
    if (!user) return;
    if (next === currentLanguage) return;

    const previous = currentLanguage;
    setIsSaving(true);
    try {
      const updated = await updateCurrentUserPreferences({ preferredLanguage: next });
      updateUser({ preferredLanguage: updated.preferredLanguage });
      await i18n.changeLanguage(normalizeLanguage(updated.preferredLanguage));
    } catch {
      await i18n.changeLanguage(previous);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="flex items-center gap-2">
      <span className={compact ? "sr-only" : "text-xs text-muted-foreground"}>
        {t("language.label")}
      </span>
      <Select
        className={compact ? "w-[4.5rem]" : "w-[120px]"}
        value={currentLanguage}
        disabled={!user || isSaving}
        onChange={(event) =>
          void onChangeLanguage(event.target.value as SupportedLanguage)
        }
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

