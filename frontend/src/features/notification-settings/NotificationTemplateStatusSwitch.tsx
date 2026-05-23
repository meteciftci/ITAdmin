import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { Switch } from "@/components/ui/switch";
import {
  NOTIFICATION_TEMPLATES_QUERY_KEY,
  updateNotificationTemplateStatus,
} from "@/features/notification-templates/api";
type Props = {
  templateId: string;
  isEnabled: boolean;
  canUpdate: boolean;
};

export function NotificationTemplateStatusSwitch({
  templateId,
  isEnabled,
  canUpdate,
}: Props) {
  const { t } = useTranslation(["notificationTemplates", "common"]);
  const queryClient = useQueryClient();
  const [checked, setChecked] = useState(isEnabled);

  const mutation = useMutation({
    mutationFn: (nextEnabled: boolean) =>
      updateNotificationTemplateStatus(templateId, nextEnabled),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: NOTIFICATION_TEMPLATES_QUERY_KEY });
      toast.success(t("notificationTemplates:messages.statusUpdateSuccess"));
    },
  });

  if (!canUpdate) {
    return null;
  }

  return (
    <Switch
      checked={checked}
      disabled={mutation.isPending}
      aria-label={t("notificationTemplates:fields.isEnabled")}
      onCheckedChange={(nextEnabled) => {
        const previous = checked;
        setChecked(nextEnabled);
        mutation.mutate(nextEnabled, {
          onError: () => {
            setChecked(previous);
          },
        });
      }}
    />
  );
}
