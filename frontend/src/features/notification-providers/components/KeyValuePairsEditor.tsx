import { Plus, Trash2 } from "lucide-react";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { NotificationKeyValuePair } from "@/features/notification-providers/types";

type Props = {
  label: string;
  pairs: NotificationKeyValuePair[];
  onChange: (pairs: NotificationKeyValuePair[]) => void;
  disabled?: boolean;
};

export function KeyValuePairsEditor({ label, pairs, onChange, disabled = false }: Props) {
  const { t } = useTranslation("notificationProviders");

  const updatePair = (index: number, field: "key" | "value", value: string) => {
    const next = pairs.map((pair, pairIndex) =>
      pairIndex === index ? { ...pair, [field]: value } : pair,
    );
    onChange(next);
  };

  const addPair = () => {
    onChange([...pairs, { key: "", value: "" }]);
  };

  const removePair = (index: number) => {
    onChange(pairs.filter((_, pairIndex) => pairIndex !== index));
  };

  return (
    <div className="space-y-2">
      <p className="text-sm font-medium">{label}</p>
      {pairs.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("common.noEntries")}</p>
      ) : null}
      {pairs.map((pair, index) => (
        <div key={`${label}-${index}`} className="flex flex-col gap-2 sm:flex-row sm:items-center">
          <Input
            value={pair.key}
            onChange={(event) => updatePair(index, "key", event.target.value)}
            placeholder={t("common.keyPlaceholder")}
            disabled={disabled}
          />
          <Input
            value={pair.value}
            onChange={(event) => updatePair(index, "value", event.target.value)}
            placeholder={t("common.valuePlaceholder")}
            disabled={disabled}
          />
          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => removePair(index)}
            disabled={disabled}
            aria-label={t("common.removeEntry")}
          >
            <Trash2 className="size-4" />
          </Button>
        </div>
      ))}
      <Button type="button" variant="outline" size="sm" onClick={addPair} disabled={disabled}>
        <Plus className="mr-2 size-4" />
        {t("common.addEntry")}
      </Button>
    </div>
  );
}
