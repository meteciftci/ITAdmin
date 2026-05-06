import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";

type StatusBadgeProps = {
  isActive: boolean;
  activeText?: ReactNode;
  passiveText?: ReactNode;
};

export function StatusBadge({ isActive, activeText, passiveText }: StatusBadgeProps) {
  const { t } = useTranslation(["common"]);
  return (
    <Badge variant={isActive ? "success" : "outline"}>
      {isActive ? (activeText ?? t("common:status.active")) : (passiveText ?? t("common:status.passive"))}
    </Badge>
  );
}
