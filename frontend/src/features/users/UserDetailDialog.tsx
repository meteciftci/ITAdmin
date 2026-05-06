import { DetailDialog } from "@/components/common/DetailDialog";
import { DateTimeText } from "@/components/common/DateTimeText";
import { RoleBadgeList } from "@/components/common/RoleBadgeList";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Separator } from "@/components/ui/separator";
import type { UserDetail } from "@/features/users/types";
import { useTranslation } from "react-i18next";

type UserDetailDialogProps = {
  user: UserDetail | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export function UserDetailDialog({ user, open, onOpenChange }: UserDetailDialogProps) {
  const { t } = useTranslation(["users", "common"]);

  return (
    <DetailDialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("users:detail.title")}
      description={t("users:description")}
    >
      {user ? (
        <div className="space-y-3 text-sm">
          <div className="grid gap-3 md:grid-cols-2">
            <p>
              <span className="text-muted-foreground">{t("users:detail.displayName")}:</span>{" "}
              {user.displayName}
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.userName")}:</span>{" "}
              {user.userName}
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.email")}:</span>{" "}
              {user.email || "-"}
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.status")}:</span>{" "}
              <StatusBadge isActive={user.isActive} />
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.directorySource")}:</span>{" "}
              {user.directorySource || "-"}
            </p>
            <p className="break-all font-mono text-xs md:text-sm">
              <span className="font-sans text-muted-foreground">{t("users:detail.directoryObjectId")}:</span>{" "}
              {user.directoryObjectId || "-"}
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.nationalIdMasked")}:</span>{" "}
              {user.nationalIdMasked || "-"}
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.lastLogin")}:</span>{" "}
              <DateTimeText value={user.lastLoginAt} />
            </p>
          </div>
          <Separator />
          <p>
            <span className="text-muted-foreground">{t("users:detail.roles")}:</span>{" "}
            <RoleBadgeList roles={user.roles} />
          </p>
          <Separator />
          <div className="grid gap-3 md:grid-cols-2">
            <p>
              <span className="text-muted-foreground">{t("users:detail.createdAt")}:</span>{" "}
              <DateTimeText value={user.createdAt} />
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.createdBy")}:</span>{" "}
              {user.createdBy || "-"}
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.updatedAt")}:</span>{" "}
              <DateTimeText value={user.updatedAt} />
            </p>
            <p>
              <span className="text-muted-foreground">{t("users:detail.updatedBy")}:</span>{" "}
              {user.updatedBy || "-"}
            </p>
          </div>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t("common:notAvailable")}</p>
      )}
    </DetailDialog>
  );
}
