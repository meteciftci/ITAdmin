import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";

type Props = {
  isLockedOut: boolean;
};

export function AdLockStatusBadge({ isLockedOut }: Props) {
  const { t } = useTranslation("adManagement");

  return (
    <Badge variant={isLockedOut ? "destructive" : "outline"}>
      {isLockedOut ? t("users.status.locked") : t("users.status.unlocked")}
    </Badge>
  );
}
