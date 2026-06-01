import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

import { CodeBadge } from "@/components/common/CodeBadge";
import { DateTimeText } from "@/components/common/DateTimeText";
import { Badge } from "@/components/ui/badge";
import { AdOperationLogSnapshotDetail } from "@/features/ad-management/components/AdOperationLogSnapshotDetail";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { getAdOperationStatusBadgeVariant } from "@/features/ad-management/ad-operation-log-columns";
import {
  parseAdOperationErrorMessage,
  parseRequestSummaryChangeStatus,
} from "@/features/ad-management/parse-ad-operation-error-message";
import type { AdOperationLogDetail } from "@/features/ad-management/operation-logs-types";

type AdOperationLogDetailDialogProps = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  detail: AdOperationLogDetail | null | undefined;
  isLoading: boolean;
  getOperationLabel: (operationType: string) => string;
  getStatusLabel: (status: string, changeStatus: string | null) => string;
};

function DetailRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="space-y-1">
      <p className="text-xs text-muted-foreground">{label}</p>
      <div className="min-h-5 whitespace-pre-wrap break-words">{value ?? "-"}</div>
    </div>
  );
}

export function AdOperationLogDetailDialog({
  open,
  onOpenChange,
  detail,
  isLoading,
  getOperationLabel,
  getStatusLabel,
}: AdOperationLogDetailDialogProps) {
  const { t } = useTranslation(["adOperationLogs", "common"]);

  const changeStatus = parseRequestSummaryChangeStatus(detail?.requestSummaryJson);
  const parsedError = parseAdOperationErrorMessage(detail?.errorMessage);

  const diagnosticRows =
    parsedError?.kind === "structured"
      ? [
          { label: t("diagnostic.code"), value: parsedError.diagnostic.code },
          { label: t("diagnostic.operation"), value: parsedError.diagnostic.operation },
          { label: t("diagnostic.step"), value: parsedError.diagnostic.step },
          { label: t("diagnostic.attribute"), value: parsedError.diagnostic.attribute },
          { label: t("diagnostic.reason"), value: parsedError.diagnostic.normalizedReason },
          { label: t("diagnostic.message"), value: parsedError.diagnostic.message },
          {
            label: t("diagnostic.ldapResultCode"),
            value:
              parsedError.diagnostic.ldapResultCode !== undefined
                ? String(parsedError.diagnostic.ldapResultCode)
                : undefined,
          },
          {
            label: t("diagnostic.ldapExceptionErrorCode"),
            value:
              parsedError.diagnostic.ldapExceptionErrorCode !== undefined
                ? String(parsedError.diagnostic.ldapExceptionErrorCode)
                : undefined,
          },
          {
            label: t("diagnostic.partialUpdate"),
            value:
              parsedError.diagnostic.partialUpdate !== undefined
                ? String(parsedError.diagnostic.partialUpdate)
                : undefined,
          },
          { label: t("diagnostic.rollbackStatus"), value: parsedError.diagnostic.rollbackStatus },
          {
            label: t("diagnostic.targetObjectGuid"),
            value: parsedError.diagnostic.targetObjectGuid,
          },
          {
            label: t("diagnostic.ldapDiagnosticMessage"),
            value: parsedError.diagnostic.ldapDiagnosticMessage,
          },
        ]
      : [];

  return (
    <Dialog open={open}>
      <DialogContent onOpenChange={onOpenChange} className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>{t("detail.title")}</DialogTitle>
          <DialogDescription>{t("detail.description")}</DialogDescription>
        </DialogHeader>

        <div className="max-h-[75vh] space-y-6 overflow-y-auto p-4 text-sm">
          {isLoading ? (
            <p className="text-muted-foreground">{t("common:table.loading")}</p>
          ) : null}

          {!isLoading && detail ? (
            <>
              <section className="space-y-3">
                <h3 className="text-sm font-medium">{t("detail.sections.general")}</h3>
                <div className="grid gap-3 md:grid-cols-2">
                  <DetailRow
                    label={t("detail.createdAt")}
                    value={<DateTimeText value={detail.createdAt} />}
                  />
                  <DetailRow
                    label={t("detail.operation")}
                    value={
                      <Badge variant="outline">{getOperationLabel(detail.operationType)}</Badge>
                    }
                  />
                  <DetailRow
                    label={t("detail.status")}
                    value={
                      <Badge
                        variant={getAdOperationStatusBadgeVariant(detail.status, changeStatus)}
                      >
                        {getStatusLabel(detail.status, changeStatus)}
                      </Badge>
                    }
                  />
                  <DetailRow
                    label={t("detail.target")}
                    value={
                      detail.targetSamAccountName ?? detail.targetObjectGuid ?? (
                        <span className="text-muted-foreground">-</span>
                      )
                    }
                  />
                  <DetailRow
                    label={t("detail.targetDn")}
                    value={
                      detail.targetDistinguishedName ? (
                        <span className="font-mono text-xs break-all">
                          {detail.targetDistinguishedName}
                        </span>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )
                    }
                  />
                  <DetailRow label={t("detail.actor")} value={detail.actorUserName} />
                  <DetailRow label={t("detail.domainController")} value={detail.domainController} />
                  <DetailRow label={t("detail.ipAddress")} value={detail.ipAddress} />
                  <DetailRow
                    label={t("detail.correlationId")}
                    value={
                      detail.correlationId ? (
                        <CodeBadge>{detail.correlationId}</CodeBadge>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )
                    }
                  />
                  <DetailRow label={t("detail.userAgent")} value={detail.userAgent} />
                </div>
              </section>

              <section className="space-y-3 border-t pt-4">
                <h3 className="text-sm font-medium">{t("detail.sections.error")}</h3>
                {parsedError?.kind === "plainText" ? (
                  <p className="whitespace-pre-wrap break-words rounded-md border bg-muted/30 p-3">
                    {parsedError.text}
                  </p>
                ) : null}
                {parsedError?.kind === "structured" ? (
                  <div className="grid gap-3 md:grid-cols-2">
                    {diagnosticRows.map((row) => (
                      <DetailRow key={row.label} label={row.label} value={row.value} />
                    ))}
                  </div>
                ) : null}
                {!parsedError && !detail.errorMessage ? (
                  <span className="text-muted-foreground">{t("detail.none")}</span>
                ) : null}
              </section>

              <AdOperationLogSnapshotDetail
                beforeSnapshotJson={detail.beforeSnapshotJson}
                afterSnapshotJson={detail.afterSnapshotJson}
                requestSummaryJson={detail.requestSummaryJson}
              />
            </>
          ) : null}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
            {t("common:actions.close")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
