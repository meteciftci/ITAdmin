import type { TFunction } from "i18next";
import {
  Ban,
  CircleAlert,
  CircleCheck,
  Clock3,
  Mail,
  MessageSquareText,
  RefreshCw,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";

type StatusBadgeVariant =
  | "default"
  | "secondary"
  | "destructive"
  | "outline"
  | "success"
  | "warning";

function getStatusVariant(status: string): StatusBadgeVariant {
  switch (status) {
    case "Sent":
      return "success";
    case "Failed":
      return "destructive";
    case "Processing":
      return "warning";
    case "Cancelled":
      return "secondary";
    default:
      return "outline";
  }
}

export function NotificationDeliveryStatusBadge({
  status,
  t,
}: {
  status: string;
  t: TFunction;
}) {
  const normalizedStatus = status.toLowerCase();
  const iconClassName = "size-3.5 shrink-0";
  const icon =
    status === "Sent" ? (
      <CircleCheck className={iconClassName} aria-hidden />
    ) : status === "Failed" ? (
      <CircleAlert className={iconClassName} aria-hidden />
    ) : status === "Processing" ? (
      <RefreshCw className={iconClassName} aria-hidden />
    ) : status === "Cancelled" ? (
      <Ban className={iconClassName} aria-hidden />
    ) : (
      <Clock3 className={iconClassName} aria-hidden />
    );

  return (
    <Badge variant={getStatusVariant(status)} className="gap-1.5 whitespace-nowrap">
      {icon}
      {t(`notificationOutbox:statuses.${normalizedStatus}`, {
        defaultValue: status,
      })}
    </Badge>
  );
}

export function NotificationChannelBadge({
  channel,
  t,
}: {
  channel: string;
  t: TFunction;
}) {
  const isEmail = channel.toLowerCase() === "email";
  const label = isEmail
    ? t("common:channels.email")
    : channel.toLowerCase() === "sms"
      ? t("common:channels.sms")
      : channel;

  return (
    <Badge variant="outline" className="gap-1.5 whitespace-nowrap">
      {isEmail ? (
        <Mail className="size-3.5" aria-hidden />
      ) : (
        <MessageSquareText className="size-3.5" aria-hidden />
      )}
      {label}
    </Badge>
  );
}
