import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { buttonVariants } from "@/components/ui/button-variants";
import { cn } from "@/lib/utils";

import { DetailDialog } from "@/components/common/DetailDialog";
import { AdUserAccountSummaryCards } from "@/features/ad-management/components/ad-user-detail/AdUserAccountSummaryCards";
import { AdUserBasicInfoSection } from "@/features/ad-management/components/ad-user-detail/AdUserBasicInfoSection";
import { AdUserGroupsSummarySection } from "@/features/ad-management/components/ad-user-detail/AdUserGroupsSummarySection";
import { AdUserMappedAttributesSection } from "@/features/ad-management/components/ad-user-detail/AdUserMappedAttributesSection";
import { AdUserTechnicalInfoSection } from "@/features/ad-management/components/ad-user-detail/AdUserTechnicalInfoSection";
import type { MappedAttributeDisplayFilter } from "@/features/ad-management/ad-user-detail-utils";
import type { AdUserDetail } from "@/features/ad-management/types";
import { useState } from "react";

const detailDialogActionButtonClass = cn(
  buttonVariants({ size: "sm" }),
  "inline-flex h-8 min-h-8 items-center justify-center px-3 text-sm",
);

const editUserButtonClass = cn(
  detailDialogActionButtonClass,
  "border border-amber-500/30 bg-amber-500/15 text-amber-700 hover:bg-amber-500/25",
  "dark:bg-amber-500/15 dark:text-amber-300 dark:hover:bg-amber-500/25",
);

type Props = {
  user: AdUserDetail | null;
  open: boolean;
  canUpdateUser: boolean;
  onOpenChange: (open: boolean) => void;
};

export function AdUserDetailDialog({
  user,
  open,
  canUpdateUser,
  onOpenChange,
}: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const navigate = useNavigate();
  const [mappedAttributesFilter, setMappedAttributesFilter] =
    useState<MappedAttributeDisplayFilter>("filled");

  const headerActions =
    user && canUpdateUser ? (
      <button
        type="button"
        className={editUserButtonClass}
        onClick={() => {
          onOpenChange(false);
          navigate(`/ad-management/users/${user.id}/edit`);
        }}
      >
        {t("adManagement:users.actions.edit")}
      </button>
    ) : undefined;

  return (
    <DetailDialog
      open={open}
      onOpenChange={onOpenChange}
      title={t("adManagement:users.detail.dialogTitle")}
      description={user?.displayName ?? user?.samAccountName ?? undefined}
      actions={headerActions}
    >
      {user ? (
        <div className="max-h-[70vh] space-y-4 overflow-y-auto text-sm">
          <AdUserAccountSummaryCards user={user} />
          <AdUserBasicInfoSection user={user} />
          <AdUserTechnicalInfoSection user={user} />
          <AdUserMappedAttributesSection
            user={user}
            filter={mappedAttributesFilter}
            onFilterChange={setMappedAttributesFilter}
          />
          <AdUserGroupsSummarySection user={user} />
        </div>
      ) : null}
    </DetailDialog>
  );
}
