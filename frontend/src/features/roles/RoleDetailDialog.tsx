import { Badge } from "@/components/ui/badge";
import { DetailDialog } from "@/components/common/DetailDialog";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Separator } from "@/components/ui/separator";
import type { RoleDetail } from "@/features/roles/types";
import { DateTimeText } from "@/components/common/DateTimeText";
import { useTranslation } from "react-i18next";

type RoleDetailDialogProps = {
  role: RoleDetail | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export function RoleDetailDialog({ role, open, onOpenChange }: RoleDetailDialogProps) {
  const { t } = useTranslation(["roles", "common"]);
  return (
    <DetailDialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("roles:detail.title")}
      description={t("roles:description")}
    >
      {!role ? (
        <p className="text-sm text-muted-foreground">{t("common:notAvailable")}</p>
      ) : (
        <div className="space-y-4 text-sm">
          {role.isSystem ? (
            <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-700">
              {t("roles:detail.systemNotice")}
            </div>
          ) : null}
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.name")}</p>
              <p>{role.name}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.code")}</p>
              <p className="font-mono text-xs md:text-sm">{role.code}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.description")}</p>
              <p>{role.description || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.type")}</p>
              <Badge variant={role.isSystem ? "warning" : "secondary"}>
                {role.isSystem ? t("roles:type.system") : t("roles:type.custom")}
              </Badge>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.status")}</p>
              <StatusBadge isActive={role.isActive} />
            </div>
          </div>
          <Separator />
          <div className="space-y-2">
            <p className="font-medium">{t("roles:detail.permissions")}</p>
            {role.permissions.length ? (
              <div className="max-h-48 overflow-y-auto rounded-lg border p-2">
                <div className="flex flex-wrap gap-1">
                  {role.permissions.map((permission) => (
                    <Badge key={permission.id} variant="outline">
                      {permission.code}
                    </Badge>
                  ))}
                </div>
              </div>
            ) : (
              <p className="text-muted-foreground">{t("roles:assignPermissions.noPermissions")}</p>
            )}
          </div>
          <Separator />
          <div className="grid gap-3 md:grid-cols-2">
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.createdAt")}</p>
              <DateTimeText value={role.createdAt} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.createdBy")}</p>
              <p>{role.createdBy || "-"}</p>
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.updatedAt")}</p>
              <DateTimeText value={role.updatedAt} />
            </div>
            <div className="space-y-1">
              <p className="text-xs text-muted-foreground">{t("roles:detail.updatedBy")}</p>
              <p>{role.updatedBy || "-"}</p>
            </div>
          </div>
        </div>
      )}
    </DetailDialog>
  );
}
