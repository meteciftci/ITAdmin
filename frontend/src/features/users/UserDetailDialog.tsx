import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DateTimeText } from "@/components/common/DateTimeText";
import { Separator } from "@/components/ui/separator";
import type { UserDetail } from "@/features/users/types";
import { useTranslation } from "react-i18next";

type UserDetailDialogProps = {
  user: UserDetail;
};

export function UserDetailDialog({ user }: UserDetailDialogProps) {
  const { t } = useTranslation(["users", "common"]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("users:detail.title")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <div className="grid gap-2 md:grid-cols-2">
          <p>
            <span className="font-medium">{t("users:detail.displayName")}:</span>{" "}
            {user.displayName}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.userName")}:</span>{" "}
            {user.userName}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.email")}:</span>{" "}
            {user.email || "-"}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.status")}:</span>{" "}
            {user.isActive ? t("common:status.active") : t("common:status.passive")}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.directorySource")}:</span>{" "}
            {user.directorySource || "-"}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.directoryObjectId")}:</span>{" "}
            {user.directoryObjectId || "-"}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.nationalIdMasked")}:</span>{" "}
            {user.nationalIdMasked || "-"}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.lastLogin")}:</span>{" "}
            <DateTimeText value={user.lastLoginAt} />
          </p>
        </div>
        <Separator />
        <p>
          <span className="font-medium">{t("users:detail.roles")}:</span>{" "}
          {user.roles.length ? user.roles.join(", ") : "-"}
        </p>
        <Separator />
        <div className="grid gap-2 md:grid-cols-2">
          <p>
            <span className="font-medium">{t("users:detail.createdAt")}:</span>{" "}
            <DateTimeText value={user.createdAt} />
          </p>
          <p>
            <span className="font-medium">{t("users:detail.createdBy")}:</span>{" "}
            {user.createdBy || "-"}
          </p>
          <p>
            <span className="font-medium">{t("users:detail.updatedAt")}:</span>{" "}
            <DateTimeText value={user.updatedAt} />
          </p>
          <p>
            <span className="font-medium">{t("users:detail.updatedBy")}:</span>{" "}
            {user.updatedBy || "-"}
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
