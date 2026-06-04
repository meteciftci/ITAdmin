import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { DateTimeText } from "@/components/common/DateTimeText";
import { SectionCard } from "@/components/common/SectionCard";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  invalidateAdUserDetailRelatedQueries,
  updateAdUserAccountExpiration,
} from "@/features/ad-management/api";
import type { AdUserDetail } from "@/features/ad-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

type Props = {
  user: AdUserDetail;
  canUpdate: boolean;
};

function toDateInputValue(accountExpiresAt: string | null): string {
  if (!accountExpiresAt) {
    return "";
  }

  const parsed = new Date(accountExpiresAt);
  if (Number.isNaN(parsed.getTime())) {
    return "";
  }

  return parsed.toISOString().slice(0, 10);
}

export function AdUserAccountExpirationSection({ user, canUpdate }: Props) {
  const { t } = useTranslation(["adManagement", "common"]);
  const queryClient = useQueryClient();
  const neverExpires = !user.accountExpiresAt;
  const [editing, setEditing] = useState(false);
  const [formNeverExpires, setFormNeverExpires] = useState(neverExpires);
  const [expiresAt, setExpiresAt] = useState(toDateInputValue(user.accountExpiresAt));

  function startEditing() {
    setFormNeverExpires(neverExpires);
    setExpiresAt(toDateInputValue(user.accountExpiresAt));
    setEditing(true);
  }

  function cancelEditing() {
    setFormNeverExpires(neverExpires);
    setExpiresAt(toDateInputValue(user.accountExpiresAt));
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
        getApiErrorMessage(
          error,
          t("adManagement:users.detail.accountExpiration.updateFailed"),
        ),
      );
    },
  });

  function handleSave() {
    if (!formNeverExpires && !expiresAt.trim()) {
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
          <Button type="button" variant="outline" size="sm" onClick={startEditing}>
            {t("common:actions.edit")}
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
              <DateTimeText value={user.accountExpiresAt} />
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
                  setExpiresAt("");
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
            <div className="space-y-1.5">
              <Label htmlFor={`account-expires-at-${user.id}`}>
                {t("adManagement:users.detail.accountExpiration.expiresAt")}
              </Label>
              <Input
                id={`account-expires-at-${user.id}`}
                type="date"
                value={expiresAt}
                onChange={(event) => setExpiresAt(event.target.value)}
                disabled={expirationMutation.isPending}
              />
            </div>
          ) : null}

          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              size="sm"
              onClick={handleSave}
              disabled={expirationMutation.isPending}
            >
              {t("common:actions.save")}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={cancelEditing}
              disabled={expirationMutation.isPending}
            >
              {t("common:actions.cancel")}
            </Button>
          </div>
        </div>
      )}
    </SectionCard>
  );
}
