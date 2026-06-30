import { useTranslation } from "react-i18next";

import { Badge } from "@/components/ui/badge";
import type { BadgeVariants } from "@/components/ui/badge-variants";
import { getRequestItemStatusLabel } from "@/features/license-management/enum-labels";
import type { LicenseRequestItemStatus } from "@/features/license-management/types";
import { cn } from "@/lib/utils";

type Props = {
  status: LicenseRequestItemStatus;
  className?: string;
};

function resolveVariant(status: LicenseRequestItemStatus): NonNullable<BadgeVariants["variant"]> {
  switch (status) {
    case "Pending":
    case "PartiallyFulfilled":
      return "warning";
    case "InReview":
      return "info";
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

export function LicenseRequestItemStatusBadge({ status, className }: Props) {
  const { t } = useTranslation(["licenseManagement"]);

  return (
    <Badge variant={resolveVariant(status)} className={cn(className)}>
      {getRequestItemStatusLabel(t, status)}
    </Badge>
  );
}
