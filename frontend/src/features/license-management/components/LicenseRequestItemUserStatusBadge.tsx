import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";
import type { BadgeVariants } from "@/components/ui/badge-variants";
import { getRequestItemUserStatusLabel } from "@/features/license-management/enum-labels";
import type { LicenseRequestItemUserStatus } from "@/features/license-management/types";
import { cn } from "@/lib/utils";

type Props = {
  status: LicenseRequestItemUserStatus;
  className?: string;
};

function resolveVariant(status: LicenseRequestItemUserStatus): NonNullable<BadgeVariants["variant"]> {
  switch (status) {
    case "Pending":
      return "warning";
    case "Approved":
    case "Fulfilled":
      return "success";
    case "Rejected":
      return "destructive";
    case "Cancelled":
      return "outline";
    default:
      return "outline";
  }
}

export function LicenseRequestItemUserStatusBadge({ status, className }: Props) {
  const { t } = useTranslation(["licenseManagement"]);

  return (
    <Badge variant={resolveVariant(status)} className={cn(className)}>
      {getRequestItemUserStatusLabel(t, status)}
    </Badge>
  );
}
