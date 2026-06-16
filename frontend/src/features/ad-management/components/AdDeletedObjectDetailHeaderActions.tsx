import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import {
  adDetailActionButtonSizingClass,
  adDetailOutlineButtonClass,
} from "@/features/ad-management/ad-user-detail-button-styles";
import { buildAdDeletedObjectRestorePath } from "@/features/ad-management/ad-deleted-object-detail-path";
import { canRestoreDeletedObject } from "@/features/ad-management/ad-deleted-object-restore-eligibility";
import type { AdDeletedObjectDetail } from "@/features/ad-management/types";
import { canAccess } from "@/lib/permissions";
import { useAuthStore } from "@/features/auth/auth-store";

type Props = {
  detail: AdDeletedObjectDetail;
  returnPath: string;
  isFetching: boolean;
  onRefresh: () => void;
};

export function AdDeletedObjectDetailHeaderActions({
  detail,
  returnPath,
  isFetching,
  onRefresh,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);

  const canRestore =
    canAccess(user, "AdManagement.DeletedObjects.Restore")
    && canRestoreDeletedObject(detail);

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Link to={returnPath} className={adDetailOutlineButtonClass}>
        {t("common:actions.back")}
      </Link>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className={adDetailActionButtonSizingClass}
        onClick={onRefresh}
        disabled={isFetching}
      >
        {t("common:actions.refresh")}
      </Button>
      {canRestore ? (
        <Button
          type="button"
          size="sm"
          className={adDetailActionButtonSizingClass}
          onClick={() => {
            navigate(buildAdDeletedObjectRestorePath(detail.id), {
              state: { returnPath },
            });
          }}
        >
          {t("adManagement:deletedObjects.actions.restore")}
        </Button>
      ) : null}
    </div>
  );
}
