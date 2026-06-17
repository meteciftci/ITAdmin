import { format, parseISO } from "date-fns";
import { enUS, tr } from "date-fns/locale";
import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { DatePicker } from "@/components/common/DatePicker";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { AD_USER_FORM_ACTIONS_CLASSNAME } from "@/features/ad-management/ad-form-actions";
import { adUserDetailEditButtonClass } from "@/features/ad-management/ad-user-detail-button-styles";
import {
  invalidateAdUserDetailRelatedQueries,
  updateAdUserAccountExpiration,
} from "@/features/ad-management/api";
import type { AdUserDetail } from "@/features/ad-management/types";
import { getAdManagementApiErrorMessage } from "@/features/ad-management/ad-management-api-message";

type Props = {
  user: AdUserDetail;
  canUpdate: boolean;
};

function formatAccountExpiresDateLabel(
  accountExpiresDate: string,
  locale: "tr" | "en",
): string {
  const parsed = parseISO(accountExpiresDate);
  if (Number.isNaN(parsed.getTime())) {
    return accountExpiresDate;
  }

  const dateLocale = locale === "tr" ? tr : enUS;
  const formatPattern = locale === "tr" ? "dd.MM.yyyy" : "MM/dd/yyyy";
  return format(parsed, formatPattern, { locale: dateLocale });
}

export function AdUserAccountExpirationSection({ user, canUpdate }: Props) {
  const { t, i18n } = useTranslation(["adManagement", "common"]);
  const locale = i18n.language.startsWith("tr") ? "tr" : "en";
  const queryClient = useQueryClient();
  const neverExpires = !user.accountExpiresDate;
  const [editing, setEditing] = useState(false);
  const [formNeverExpires, setFormNeverExpires] = useState(neverExpires);
  const [expiresAt, setExpiresAt] = useState(user.accountExpiresDate);

  function startEditing() {
    setFormNeverExpires(neverExpires);
    setExpiresAt(user.accountExpiresDate);
    setEditing(true);
  }

  function cancelEditing() {
    setFormNeverExpires(neverExpires);
    setExpiresAt(user.accountExpiresDate);
    setEditing(false);
  }

  const expirationMutation = useMutation({
    mutationFn: () =>
      updateAdUserAccountExpiration(user.id, {
        neverExpires: formNeverExpires,
        expiresAt: formNeverExpires ? null : expiresAt,
      }),
    onSuccess: async (response) => {
      if (!response.success) {
        toast.error(t("adManagement:users.detail.accountExpiration.updateFailed"));
        return;
      }

      await invalidateAdUserDetailRelatedQueries(queryClient, user.id);
      toast.success(t("adManagement:users.detail.accountExpiration.updated"));
      setEditing(false);
    },
    onError: (error) => {
      toast.error(
        getAdManagementApiErrorMessage(
          error,
          t,
          "adManagement:users.detail.accountExpiration.updateFailed",
        ),
      );
    },
  });

  function handleSave() {
    if (!formNeverExpires && !expiresAt?.trim()) {
      toast.error(t("adManagement:users.detail.accountExpiration.dateRequired"));
      return;
    }

    expirationMutation.mutate();
  }

  return (
    <SectionCard
      title={t("adManagement:users.detail.accountExpiration.title")}
      actions={
        canUpdate && !editing ? (
          <Button
            type="button"
            className={adUserDetailEditButtonClass}
            onClick={startEditing}
          >
            {t("adManagement:users.actions.edit")}
          </Button>
        ) : null
      }
    >
      {!editing ? (
        <p className="text-sm">
          {neverExpires ? (
            t("adManagement:users.detail.accountExpiration.neverExpires")
          ) : (
            <>
              <span className="text-muted-foreground">
                {t("adManagement:users.detail.accountExpiration.expiresAt")}:{" "}
              </span>
              <span className="font-medium">
                {formatAccountExpiresDateLabel(user.accountExpiresDate!, locale)}
              </span>
            </>
          )}
        </p>
      ) : (
        <div className="space-y-4">
          <div className="space-y-2">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="radio"
                name={`account-expiration-mode-${user.id}`}
                checked={formNeverExpires}
                onChange={() => {
                  setFormNeverExpires(true);
                  setExpiresAt(null);
                }}
                disabled={expirationMutation.isPending}
              />
              {t("adManagement:users.detail.accountExpiration.neverExpires")}
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="radio"
                name={`account-expiration-mode-${user.id}`}
                checked={!formNeverExpires}
                onChange={() => setFormNeverExpires(false)}
                disabled={expirationMutation.isPending}
              />
              {t("adManagement:users.detail.accountExpiration.expiresOnDate")}
            </label>
          </div>

          {!formNeverExpires ? (
            <DatePicker
              id={`account-expires-at-${user.id}`}
              value={expiresAt}
              onChange={setExpiresAt}
              placeholder={t("adManagement:users.detail.accountExpiration.datePlaceholder")}
              clearLabel={t("common:actions.clear")}
              locale={locale}
              disabled={expirationMutation.isPending}
            />
          ) : null}

          <div className={AD_USER_FORM_ACTIONS_CLASSNAME}>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={cancelEditing}
              disabled={expirationMutation.isPending}
            >
              {t("common:actions.cancel")}
            </Button>
            <Button
              type="button"
              size="sm"
              onClick={handleSave}
              disabled={expirationMutation.isPending}
            >
              {t("common:actions.save")}
            </Button>
          </div>
        </div>
      )}
    </SectionCard>
  );
}
