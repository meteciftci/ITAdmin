import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";

import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { assignLicenseSeat, getLicensePackages } from "@/features/license-management/api";
import { PersonFields } from "@/features/license-management/components/license-seat-person-fields";
import {
  EMPTY_PERSON,
  toPersonInput,
  type PersonDraft,
} from "@/features/license-management/components/license-seat-person";
import { getApiErrorMessage } from "@/lib/api-error";

type Props = {
  /** When set the dialog assigns from this package. When omitted it shows a package picker. */
  packageId?: string;
  onClose: () => void;
  onDone: () => void;
};

export function LicenseAssignmentDialog({ packageId, onClose, onDone }: Props) {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const [selectedPackageId, setSelectedPackageId] = useState("");
  const [draft, setDraft] = useState<PersonDraft>(EMPTY_PERSON);
  const [assignedDate, setAssignedDate] = useState("");
  const [note, setNote] = useState("");

  const needsPackage = !packageId;

  const packagesQuery = useQuery({
    queryKey: ["license-management", "packages", "assignable"],
    queryFn: () => getLicensePackages({ isActive: true, pageSize: 100 }),
    enabled: needsPackage,
  });

  const packageOptions = useMemo(
    () =>
      [...(packagesQuery.data?.items ?? [])].sort((a, b) =>
        `${a.productName}${a.purchaseTitle}`.localeCompare(`${b.productName}${b.purchaseTitle}`, "tr"),
      ),
    [packagesQuery.data],
  );

  const effectivePackageId = packageId ?? selectedPackageId;

  const mutation = useMutation({
    mutationFn: () =>
      assignLicenseSeat(effectivePackageId, {
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

  const canSubmit =
    !mutation.isPending && draft.displayName.trim().length > 0 && effectivePackageId.length > 0;

  return (
    <Dialog open>
      <DialogContent onOpenChange={(open) => (!open ? onClose() : undefined)}>
        <DialogHeader>
          <DialogTitle>{t("licenseManagement:seats.dialog.assignTitle")}</DialogTitle>
          <DialogDescription>{t("licenseManagement:seats.dialog.assignDescription")}</DialogDescription>
        </DialogHeader>
        <DialogBody className="space-y-3">
          {needsPackage ? (
            <div className="space-y-1">
              <Label htmlFor="assign-package">{t("licenseManagement:seats.dialog.packagePicker")}</Label>
              <Select
                id="assign-package"
                value={selectedPackageId}
                onChange={(event) => setSelectedPackageId(event.target.value)}
              >
                <option value="">{t("licenseManagement:seats.dialog.packagePickerPlaceholder")}</option>
                {packageOptions.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.productName} · {item.purchaseTitle} — {item.availableQuantity}/{item.quantity}{" "}
                    {t("licenseManagement:seats.dialog.seatsUnit")}
                  </option>
                ))}
              </Select>
            </div>
          ) : null}

          <PersonFields draft={draft} onChange={setDraft} />

          <div className="space-y-1">
            <Label htmlFor="assign-date">{t("licenseManagement:seats.dialog.assignedDate")}</Label>
            <Input
              id="assign-date"
              type="date"
              value={assignedDate}
              onChange={(event) => setAssignedDate(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="assign-note">{t("licenseManagement:seats.dialog.note")}</Label>
            <Textarea id="assign-note" value={note} onChange={(event) => setNote(event.target.value)} rows={2} />
          </div>
        </DialogBody>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={mutation.isPending}>
            {t("common:actions.cancel")}
          </Button>
          <Button onClick={() => mutation.mutate()} disabled={!canSubmit}>
            {t("licenseManagement:seats.actions.assign")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
