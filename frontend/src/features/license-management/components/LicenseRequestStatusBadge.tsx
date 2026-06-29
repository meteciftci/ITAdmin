import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";
import { getRequestStatusLabel } from "@/features/license-management/enum-labels";
import type { LicenseRequestStatus } from "@/features/license-management/types";
import { cn } from "@/lib/utils";

type Props = {
  status: LicenseRequestStatus;
  className?: string;
};

function resolveVariant(status: LicenseRequestStatus): "default" | "secondary" | "destructive" | "outline" {
  switch (status) {
    case "Fulfilled":
      return "default";
    case "Rejected":
    case "Cancelled":
      return "destructive";
    case "Draft":
    case "Archived":
      return "secondary";
    default:
      return "outline";
  }
}

export function LicenseRequestStatusBadge({ status, className }: Props) {
  const { t } = useTranslation(["licenseManagement"]);

  return (
    <Badge variant={resolveVariant(status)} className={cn(className)}>
      {getRequestStatusLabel(t, status)}
    </Badge>
  );
}
