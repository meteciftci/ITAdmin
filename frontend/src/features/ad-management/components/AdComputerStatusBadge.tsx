import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";

type Props = {
  isEnabled: boolean;
};

export function AdComputerStatusBadge({ isEnabled }: Props) {
  const { t } = useTranslation("adManagement");

  return (
    <Badge variant={isEnabled ? "success" : "warning"}>
      {isEnabled ? t("computers.status.enabled") : t("computers.status.disabled")}
    </Badge>
  );
}
