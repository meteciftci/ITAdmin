import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { PrettyJsonBlock } from "@/components/common/PrettyJsonBlock";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogBody, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import type { DnsOperationLogDetail } from "./types";

type Props = {
  open: boolean;
  detail?: DnsOperationLogDetail | null;
  isLoading: boolean;
  onOpenChange: (open: boolean) => void;
};

function Row({ label, value }: { label: string; value: ReactNode }) {
  return <div className="space-y-1"><p className="text-xs text-muted-foreground">{label}</p><div className="min-h-5 break-words">{value || "-"}</div></div>;
}

export function DnsOperationLogDetailDialog({ open, detail, isLoading, onOpenChange }: Props) {
  const { t } = useTranslation(["dnsManagement", "common"]);
  return <Dialog open={open}>
    <DialogContent onOpenChange={onOpenChange} className="max-w-4xl">
      <DialogHeader><DialogTitle>{t("operationLogs.detailTitle")}</DialogTitle><DialogDescription>{t("operationLogs.detailDescription")}</DialogDescription></DialogHeader>
      <DialogBody className="max-h-[75vh] space-y-6 overflow-y-auto text-sm">
        {isLoading ? <p className="text-muted-foreground">{t("common:table.loading")}</p> : null}
        {detail ? <>
          <section className="grid gap-4 md:grid-cols-2">
            <Row label={t("operationLogs.fields.createdAt")} value={<DateTimeText value={detail.createdAt} />} />
            <Row label={t("operationLogs.fields.status")} value={<Badge variant={detail.status === "Failed" ? "destructive" : detail.status === "Succeeded" ? "success" : "secondary"}>{t(`operationLogs.status.${detail.status}`, { defaultValue: detail.status })}</Badge>} />
            <Row label={t("operationLogs.fields.operation")} value={t(`operationLogs.operations.${detail.operationType}`, { defaultValue: detail.operationType })} />
            <Row label={t("operationLogs.fields.server")} value={detail.serverDisplayName} />
            <Row label={t("operationLogs.fields.zone")} value={detail.zoneName} />
            <Row label={t("operationLogs.fields.record")} value={[detail.recordName, detail.recordType].filter(Boolean).join(" · ")} />
            <Row label={t("operationLogs.fields.actor")} value={detail.actorUserName} />
            <Row label={t("operationLogs.fields.ipAddress")} value={detail.ipAddress} />
            <Row label={t("operationLogs.fields.errorCode")} value={detail.errorCode} />
            <Row label={t("operationLogs.fields.correlationId")} value={<span className="font-mono text-xs">{detail.correlationId}</span>} />
            <div className="md:col-span-2"><Row label={t("operationLogs.fields.errorMessage")} value={detail.errorMessage} /></div>
          </section>
          <section className="space-y-3 border-t pt-4"><h3 className="font-medium">{t("operationLogs.requestSummary")}</h3><PrettyJsonBlock value={detail.requestSummaryJson} /></section>
          <section className="grid gap-4 border-t pt-4 lg:grid-cols-2"><div className="space-y-3"><h3 className="font-medium">{t("operationLogs.before")}</h3><PrettyJsonBlock value={detail.beforeSnapshotJson} /></div><div className="space-y-3"><h3 className="font-medium">{t("operationLogs.after")}</h3><PrettyJsonBlock value={detail.afterSnapshotJson} /></div></section>
        </> : null}
      </DialogBody>
      <DialogFooter><Button variant="outline" onClick={() => onOpenChange(false)}>{t("common:actions.close")}</Button></DialogFooter>
    </DialogContent>
  </Dialog>;
}
