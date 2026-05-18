import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";

type Props = {
  isEnabled: boolean;
};

export function AdAccountStatusBadge({ isEnabled }: Props) {
  const { t } = useTranslation("adManagement");

  return (
    <Badge variant={isEnabled ? "success" : "warning"}>
      {isEnabled ? t("users.status.enabled") : t("users.status.disabled")}
    </Badge>
  );
}
