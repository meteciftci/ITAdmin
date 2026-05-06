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
        <div className="space-y-4 text-sm">
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.displayName")}</p>
              <p>{user.displayName}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.userName")}</p>
              <p>{user.userName}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.email")}</p>
              <p>{user.email || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.status")}</p>
              <StatusBadge isActive={user.isActive} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.directorySource")}</p>
              <p>{user.directorySource || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.directoryObjectId")}</p>
              <p className="break-all font-mono text-xs md:text-sm">{user.directoryObjectId || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.nationalIdMasked")}</p>
              <p>{user.nationalIdMasked || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.lastLogin")}</p>
              <DateTimeText value={user.lastLoginAt} />
            </div>
          </div>
          <Separator />
          <div className="space-y-1">
            <p className="text-xs text-muted-foreground">{t("users:detail.roles")}</p>
            <RoleBadgeList roles={user.roles} />
          </div>
          <Separator />
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.createdAt")}</p>
              <DateTimeText value={user.createdAt} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.createdBy")}</p>
              <p>{user.createdBy || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.updatedAt")}</p>
              <DateTimeText value={user.updatedAt} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("users:detail.updatedBy")}</p>
              <p>{user.updatedBy || "-"}</p>
            </div>
          </div>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t("common:notAvailable")}</p>
      )}
    </DetailDialog>
  );
}
