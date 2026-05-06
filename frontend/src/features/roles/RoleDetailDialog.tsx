import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import type { RoleDetail } from "@/features/roles/types";
import { DateTimeText } from "@/components/common/DateTimeText";
import { useTranslation } from "react-i18next";

type RoleDetailDialogProps = {
  role: RoleDetail | null;
  open: boolean;
  onClose: () => void;
};

export function RoleDetailDialog({ role, open, onClose }: RoleDetailDialogProps) {
  const { t } = useTranslation(["roles", "common"]);
  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={(next) => !next && onClose()}>
        <DialogHeader>
          <DialogTitle>{t("roles:detail.title")}</DialogTitle>
          <DialogDescription>
            {t("roles:description")}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 p-4 text-sm">
          {!role ? (
            <p className="text-muted-foreground">{t("common:notAvailable")}</p>
          ) : (
            <>
              {role.isSystem ? (
                <div className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-amber-700">
                  {t("roles:detail.systemNotice")}
                </div>
              ) : null}
              <div className="grid gap-2 md:grid-cols-2">
                <p>
                  <span className="font-medium">{t("roles:detail.name")}:</span> {role.name}
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.code")}:</span> {role.code}
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.description")}:</span>{" "}
                  {role.description || "-"}
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.type")}:</span>{" "}
                  <Badge variant={role.isSystem ? "warning" : "secondary"}>
                    {role.isSystem ? t("roles:type.system") : t("roles:type.custom")}
                  </Badge>
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.status")}:</span>{" "}
                  <Badge variant={role.isActive ? "success" : "outline"}>
                    {role.isActive ? t("common:status.active") : t("common:status.passive")}
                  </Badge>
                </p>
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
              <div className="grid gap-2 md:grid-cols-2">
                <p>
                  <span className="font-medium">{t("roles:detail.createdAt")}:</span>{" "}
                  <DateTimeText value={role.createdAt} />
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.createdBy")}:</span>{" "}
                  {role.createdBy || "-"}
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.updatedAt")}:</span>{" "}
                  <DateTimeText value={role.updatedAt} />
                </p>
                <p>
                  <span className="font-medium">{t("roles:detail.updatedBy")}:</span>{" "}
                  {role.updatedBy || "-"}
                </p>
              </div>
            </>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {t("common:actions.close")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
