import { useTranslation } from "react-i18next";

import { cn } from "@/lib/utils";

type AdOrganizationalUnitTechnicalFieldProps = {
  label: string;
  value?: string | null;
  fullWidth?: boolean;
  monospace?: boolean;
};

export function AdOrganizationalUnitTechnicalField({
  label,
  value,
  fullWidth = false,
  monospace = true,
}: AdOrganizationalUnitTechnicalFieldProps) {
  const { t } = useTranslation(["common"]);
  const trimmedValue = value?.trim();
  const displayValue = trimmedValue || t("common:notAvailable");

  return (
    <div className={cn("min-w-0 space-y-1", fullWidth && "md:col-span-2")}>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p
        className={cn(
          "min-w-0 text-sm text-foreground",
          "break-words break-all [overflow-wrap:anywhere] whitespace-pre-wrap",
          monospace && "font-mono text-xs",
        )}
        title={trimmedValue || undefined}
      >
        {displayValue}
      </p>
    </div>
  );
}
