import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import { DateTimeText } from "@/components/common/DateTimeText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { SectionCard } from "@/components/common/SectionCard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { LicenseAdUserPicker } from "@/features/license-management/components/LicenseAdUserPicker";
import {
  assignLicenseSeat,
  copyLicenseSeatsFromPackage,
  getLicensePackageSeatAssignments,
  getLicensePackages,
  releaseLicenseSeat,
  transferLicenseSeat,
} from "@/features/license-management/api";
import type {
  LicenseSeatAssignment,
  LicenseSeatAssignmentStatus,
  LicenseSeatPersonInput,
} from "@/features/license-management/types";
import { getApiErrorMessage } from "@/lib/api-error";

type Props = {
  packageId: string;
  productId: string;
  canManage: boolean;
};

type PersonDraft = {
  adObjectId: string | null;
  displayName: string;
  samAccountName: string | null;
  userPrincipalName: string | null;
  mail: string | null;
  nationalId: string;
  department: string | null;
  title: string | null;
};

const EMPTY_PERSON: PersonDraft = {
  adObjectId: null,
  displayName: "",
  samAccountName: null,
  userPrincipalName: null,
  mail: null,
  nationalId: "",
  department: null,
  title: null,
};

function toPersonInput(draft: PersonDraft): LicenseSeatPersonInput {
  const trimmed = draft.displayName.trim();
  return {
    adObjectId: draft.adObjectId,
    displayName: trimmed,
    samAccountName: draft.samAccountName,
    userPrincipalName: draft.userPrincipalName,
    mail: draft.mail?.trim() || null,
    nationalId: draft.nationalId.trim() || null,
    department: draft.department,
    title: draft.title,
  };
}

function seatStatusVariant(status: LicenseSeatAssignmentStatus): "default" | "secondary" | "outline" {
  if (status === "Active") return "default";
  if (status === "Transferred") return "outline";
  return "secondary";
}

export function LicensePackageSeatsSection({ packageId, productId, canManage }: Props) {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const queryClient = useQueryClient();
  const [includeHistory, setIncludeHistory] = useState(false);
  const [assignOpen, setAssignOpen] = useState(false);
  const [copyOpen, setCopyOpen] = useState(false);
  const [transferTarget, setTransferTarget] = useState<LicenseSeatAssignment | null>(null);
  const [releaseTarget, setReleaseTarget] = useState<LicenseSeatAssignment | null>(null);

  const seatsQuery = useQuery({
    queryKey: ["license-management", "packages", "seats", packageId, includeHistory],
    queryFn: () => getLicensePackageSeatAssignments(packageId, includeHistory),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["license-management", "packages", "seats", packageId] });
    void queryClient.invalidateQueries({ queryKey: ["license-management", "packages", "detail", packageId] });
  };

  const overview = seatsQuery.data;

  return (
    <SectionCard
      title={t("licenseManagement:seats.title")}
      description={t("licenseManagement:seats.description")}
      actions={
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" variant="ghost" size="sm" onClick={() => setIncludeHistory((prev) => !prev)}>
            {includeHistory ? t("licenseManagement:seats.hideHistory") : t("licenseManagement:seats.showHistory")}
          </Button>
          {canManage ? (
            <>
              <Button type="button" variant="outline" size="sm" onClick={() => setCopyOpen(true)}>
                {t("licenseManagement:seats.actions.copyFrom")}
              </Button>
              <Button type="button" size="sm" onClick={() => setAssignOpen(true)}>
                {t("licenseManagement:seats.actions.assign")}
              </Button>
            </>
          ) : null}
        </div>
      }
    >
      {seatsQuery.isLoading ? <LoadingState /> : null}
      {seatsQuery.isError ? (
        <ErrorState
          title={t("errors:generic.title")}
          description={getApiErrorMessage(seatsQuery.error, t("errors:generic.description"))}
        />
      ) : null}

      {overview ? (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">
            {t("licenseManagement:seats.summary", {
              active: overview.activeCount,
              quantity: overview.quantity,
              available: overview.availableCount,
            })}
          </p>

          {overview.assignments.length === 0 ? (
            <EmptyState title={t("licenseManagement:seats.empty")} />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] border-collapse text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="py-2 pr-3 font-medium">{t("licenseManagement:seats.columns.person")}</th>
                    <th className="py-2 pr-3 font-medium">{t("licenseManagement:seats.columns.contact")}</th>
                    <th className="py-2 pr-3 font-medium">{t("licenseManagement:seats.columns.assignedDate")}</th>
                    <th className="py-2 pr-3 font-medium">{t("common:fields.status")}</th>
                    <th className="py-2 pr-3 font-medium">{t("licenseManagement:seats.columns.note")}</th>
                    {canManage ? <th className="py-2 font-medium text-right">{t("common:fields.actions")}</th> : null}
                  </tr>
                </thead>
                <tbody>
                  {overview.assignments.map((seat) => (
                    <tr key={seat.id} className="border-b align-top last:border-0">
                      <td className="py-2 pr-3">
                        <div className="font-medium">{seat.displayName}</div>
                        {seat.nationalId ? (
                          <div className="text-xs text-muted-foreground">{seat.nationalId}</div>
                        ) : null}
                        {seat.replacesDisplayName ? (
                          <div className="text-xs text-muted-foreground">
                            {t("licenseManagement:seats.replaces", { name: seat.replacesDisplayName })}
                          </div>
                        ) : null}
                      </td>
                      <td className="py-2 pr-3">
                        <div>{seat.mail ?? seat.userPrincipalName ?? "-"}</div>
                        {seat.department ? (
                          <div className="text-xs text-muted-foreground">{seat.department}</div>
                        ) : null}
                      </td>
                      <td className="py-2 pr-3 whitespace-nowrap">
                        <DateTimeText
                          value={seat.assignedDate}
                          options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
                        />
                        {seat.releasedDate ? (
                          <div className="text-xs text-muted-foreground">
                            {t("licenseManagement:seats.until")}{" "}
                            <DateTimeText
                              value={seat.releasedDate}
                              options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
                            />
                          </div>
                        ) : null}
                      </td>
                      <td className="py-2 pr-3">
                        <Badge variant={seatStatusVariant(seat.status)}>
                          {t(`licenseManagement:seats.status.${seat.status}`)}
                        </Badge>
                      </td>
                      <td className="py-2 pr-3">
                        <span className="whitespace-pre-wrap text-xs text-muted-foreground">{seat.note ?? ""}</span>
                      </td>
                      {canManage ? (
                        <td className="py-2 text-right whitespace-nowrap">
                          {seat.status === "Active" ? (
                            <div className="inline-flex gap-2">
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                onClick={() => setTransferTarget(seat)}
                              >
                                {t("licenseManagement:seats.actions.transfer")}
                              </Button>
                              <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                onClick={() => setReleaseTarget(seat)}
                              >
                                {t("licenseManagement:seats.actions.release")}
                              </Button>
                            </div>
                          ) : null}
                        </td>
                      ) : null}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ) : null}

      {assignOpen ? (
        <AssignSeatDialog
          packageId={packageId}
          onClose={() => setAssignOpen(false)}
          onDone={() => {
            setAssignOpen(false);
            invalidate();
          }}
        />
      ) : null}

      {transferTarget ? (
        <TransferSeatDialog
          seat={transferTarget}
          onClose={() => setTransferTarget(null)}
          onDone={() => {
            setTransferTarget(null);
            invalidate();
          }}
        />
      ) : null}

      {releaseTarget ? (
        <ReleaseSeatDialog
          seat={releaseTarget}
          onClose={() => setReleaseTarget(null)}
          onDone={() => {
            setReleaseTarget(null);
            invalidate();
          }}
        />
      ) : null}

      {copyOpen ? (
        <CopySeatsDialog
          targetPackageId={packageId}
          productId={productId}
          onClose={() => setCopyOpen(false)}
          onDone={() => {
            setCopyOpen(false);
            invalidate();
          }}
        />
      ) : null}
    </SectionCard>
  );
}

function PersonFields({
  draft,
  onChange,
}: {
  draft: PersonDraft;
  onChange: (next: PersonDraft) => void;
}) {
  const { t } = useTranslation(["licenseManagement"]);
  return (
    <div className="space-y-3">
      <LicenseAdUserPicker
        label={t("licenseManagement:seats.dialog.adUser")}
        value={
          draft.adObjectId
            ? {
                adObjectId: draft.adObjectId,
                samAccountName: draft.samAccountName,
                userPrincipalName: draft.userPrincipalName,
                displayName: draft.displayName,
                department: draft.department,
                title: draft.title,
                mail: draft.mail,
                phone: null,
              }
            : null
        }
        onChange={(snapshot) =>
          onChange(
            snapshot
              ? {
                  adObjectId: snapshot.adObjectId,
                  displayName: snapshot.displayName ?? draft.displayName,
                  samAccountName: snapshot.samAccountName ?? null,
                  userPrincipalName: snapshot.userPrincipalName ?? null,
                  mail: snapshot.mail ?? null,
                  nationalId: draft.nationalId,
                  department: snapshot.department ?? null,
                  title: snapshot.title ?? null,
                }
              : { ...EMPTY_PERSON, nationalId: draft.nationalId },
          )
        }
      />
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1">
          <Label htmlFor="seat-display-name">{t("licenseManagement:seats.dialog.displayName")}</Label>
          <Input
            id="seat-display-name"
            value={draft.displayName}
            onChange={(event) => onChange({ ...draft, displayName: event.target.value })}
          />
        </div>
        <div className="space-y-1">
          <Label htmlFor="seat-national-id">{t("licenseManagement:seats.dialog.nationalId")}</Label>
          <Input
            id="seat-national-id"
            value={draft.nationalId}
            onChange={(event) => onChange({ ...draft, nationalId: event.target.value })}
          />
        </div>
        <div className="space-y-1 sm:col-span-2">
          <Label htmlFor="seat-mail">{t("licenseManagement:seats.dialog.mail")}</Label>
          <Input
            id="seat-mail"
            value={draft.mail ?? ""}
            onChange={(event) => onChange({ ...draft, mail: event.target.value || null })}
          />
        </div>
      </div>
    </div>
  );
}

function AssignSeatDialog({
  packageId,
  onClose,
  onDone,
}: {
  packageId: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const [draft, setDraft] = useState<PersonDraft>(EMPTY_PERSON);
  const [assignedDate, setAssignedDate] = useState("");
  const [note, setNote] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      assignLicenseSeat(packageId, {
        person: toPersonInput(draft),
        assignedDate: assignedDate || null,
        note: note.trim() || null,
      }),
    onSuccess: (result) => {
      if (!result.success) {
        toast.error(result.message);
        return;
      }
      toast.success(t("licenseManagement:seats.toasts.assigned"));
      onDone();
    },
    onError: (error) => toast.error(getApiErrorMessage(error, t("errors:generic.description"))),
  });

  return (
    <Dialog open>
      <DialogContent onOpenChange={(open) => (!open ? onClose() : undefined)}>
        <DialogHeader>
          <DialogTitle>{t("licenseManagement:seats.dialog.assignTitle")}</DialogTitle>
          <DialogDescription>{t("licenseManagement:seats.dialog.assignDescription")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          <PersonFields draft={draft} onChange={setDraft} />
          <div className="space-y-1">
            <Label htmlFor="seat-assigned-date">{t("licenseManagement:seats.dialog.assignedDate")}</Label>
            <Input
              id="seat-assigned-date"
              type="date"
              value={assignedDate}
              onChange={(event) => setAssignedDate(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="seat-note">{t("licenseManagement:seats.dialog.note")}</Label>
            <Textarea id="seat-note" value={note} onChange={(event) => setNote(event.target.value)} rows={2} />
          </div>
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending || draft.displayName.trim().length === 0}
          >
            {t("licenseManagement:seats.actions.assign")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function TransferSeatDialog({
  seat,
  onClose,
  onDone,
}: {
  seat: LicenseSeatAssignment;
  onClose: () => void;
  onDone: () => void;
}) {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const [draft, setDraft] = useState<PersonDraft>(EMPTY_PERSON);
  const [transferDate, setTransferDate] = useState("");
  const [note, setNote] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      transferLicenseSeat(seat.id, {
        newPerson: toPersonInput(draft),
        transferDate: transferDate || null,
        note: note.trim() || null,
      }),
    onSuccess: (result) => {
      if (!result.success) {
        toast.error(result.message);
        return;
      }
      toast.success(t("licenseManagement:seats.toasts.transferred"));
      onDone();
    },
    onError: (error) => toast.error(getApiErrorMessage(error, t("errors:generic.description"))),
  });

  return (
    <Dialog open>
      <DialogContent onOpenChange={(open) => (!open ? onClose() : undefined)}>
        <DialogHeader>
          <DialogTitle>{t("licenseManagement:seats.dialog.transferTitle")}</DialogTitle>
          <DialogDescription>
            {t("licenseManagement:seats.dialog.transferDescription", { name: seat.displayName })}
          </DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          <PersonFields draft={draft} onChange={setDraft} />
          <div className="space-y-1">
            <Label htmlFor="seat-transfer-date">{t("licenseManagement:seats.dialog.transferDate")}</Label>
            <Input
              id="seat-transfer-date"
              type="date"
              value={transferDate}
              onChange={(event) => setTransferDate(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="seat-transfer-note">{t("licenseManagement:seats.dialog.note")}</Label>
            <Textarea
              id="seat-transfer-note"
              value={note}
              onChange={(event) => setNote(event.target.value)}
              rows={2}
            />
          </div>
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t("common:actions.cancel")}
          </Button>
          <Button
            onClick={() => mutation.mutate()}
            disabled={mutation.isPending || draft.displayName.trim().length === 0}
          >
            {t("licenseManagement:seats.actions.transfer")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ReleaseSeatDialog({
  seat,
  onClose,
  onDone,
}: {
  seat: LicenseSeatAssignment;
  onClose: () => void;
  onDone: () => void;
}) {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const [releasedDate, setReleasedDate] = useState("");
  const [note, setNote] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      releaseLicenseSeat(seat.id, { releasedDate: releasedDate || null, note: note.trim() || null }),
    onSuccess: (result) => {
      if (!result.success) {
        toast.error(result.message);
        return;
      }
      toast.success(t("licenseManagement:seats.toasts.released"));
      onDone();
    },
    onError: (error) => toast.error(getApiErrorMessage(error, t("errors:generic.description"))),
  });

  return (
    <Dialog open>
      <DialogContent onOpenChange={(open) => (!open ? onClose() : undefined)}>
        <DialogHeader>
          <DialogTitle>{t("licenseManagement:seats.dialog.releaseTitle")}</DialogTitle>
          <DialogDescription>
            {t("licenseManagement:seats.dialog.releaseDescription", { name: seat.displayName })}
          </DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          <div className="space-y-1">
            <Label htmlFor="seat-released-date">{t("licenseManagement:seats.dialog.releasedDate")}</Label>
            <Input
              id="seat-released-date"
              type="date"
              value={releasedDate}
              onChange={(event) => setReleasedDate(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="seat-release-note">{t("licenseManagement:seats.dialog.note")}</Label>
            <Textarea
              id="seat-release-note"
              value={note}
              onChange={(event) => setNote(event.target.value)}
              rows={2}
            />
          </div>
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t("common:actions.cancel")}
          </Button>
          <Button variant="destructive" onClick={() => mutation.mutate()} disabled={mutation.isPending}>
            {t("licenseManagement:seats.actions.release")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function CopySeatsDialog({
  targetPackageId,
  productId,
  onClose,
  onDone,
}: {
  targetPackageId: string;
  productId: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const [sourcePackageId, setSourcePackageId] = useState("");
  const [assignedDate, setAssignedDate] = useState("");

  const packagesQuery = useQuery({
    queryKey: ["license-management", "packages", "by-product", productId],
    queryFn: () => getLicensePackages({ productId, pageSize: 100 }),
  });

  const options = useMemo(
    () => (packagesQuery.data?.items ?? []).filter((item) => item.id !== targetPackageId),
    [packagesQuery.data, targetPackageId],
  );

  const mutation = useMutation({
    mutationFn: () => copyLicenseSeatsFromPackage(targetPackageId, sourcePackageId, assignedDate || null),
    onSuccess: (result) => {
      if (!result.success) {
        toast.error(result.message);
        return;
      }
      toast.success(
        t("licenseManagement:seats.toasts.copied", {
          copied: result.copiedCount,
          skipped: result.skippedCount,
        }),
      );
      onDone();
    },
    onError: (error) => toast.error(getApiErrorMessage(error, t("errors:generic.description"))),
  });

  return (
    <Dialog open>
      <DialogContent onOpenChange={(open) => (!open ? onClose() : undefined)}>
        <DialogHeader>
          <DialogTitle>{t("licenseManagement:seats.dialog.copyTitle")}</DialogTitle>
          <DialogDescription>{t("licenseManagement:seats.dialog.copyDescription")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          <div className="space-y-1">
            <Label htmlFor="seat-source-package">{t("licenseManagement:seats.dialog.sourcePackage")}</Label>
            <Select
              id="seat-source-package"
              value={sourcePackageId}
              onChange={(event) => setSourcePackageId(event.target.value)}
            >
              <option value="">{t("licenseManagement:seats.dialog.sourcePackagePlaceholder")}</option>
              {options.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.purchaseTitle} — {item.quantity} {t("licenseManagement:seats.dialog.seatsUnit")}
                </option>
              ))}
            </Select>
          </div>
          <div className="space-y-1">
            <Label htmlFor="seat-copy-date">{t("licenseManagement:seats.dialog.assignedDate")}</Label>
            <Input
              id="seat-copy-date"
              type="date"
              value={assignedDate}
              onChange={(event) => setAssignedDate(event.target.value)}
            />
          </div>
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t("common:actions.cancel")}
          </Button>
          <Button onClick={() => mutation.mutate()} disabled={mutation.isPending || !sourcePackageId}>
            {t("licenseManagement:seats.actions.copyFrom")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
