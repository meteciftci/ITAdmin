import { useEffect, useMemo, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";

import { CheckboxField } from "@/components/common/CheckboxField";
import { FormError } from "@/components/common/FormError";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  createLicenseCompany,
  updateLicenseCompany,
} from "@/features/license-management/api";
import { validateCompanyForm } from "@/features/license-management/form-validation";
import type { LicenseCompanyDetail } from "@/features/license-management/types";
import { getLicenseManagementApiErrorMessage } from "@/features/license-management/license-api-error";

type Props = {
  mode: "create" | "edit";
  company?: LicenseCompanyDetail | null;
  onCancel: () => void;
  onSaved: () => void;
};

export function LicenseCompanyForm({ mode, company, onCancel, onSaved }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [email, setEmail] = useState("");
  const [website, setWebsite] = useState("");
  const [contactPersonName, setContactPersonName] = useState("");
  const [contactPersonPhone, setContactPersonPhone] = useState("");
  const [contactPersonEmail, setContactPersonEmail] = useState("");
  const [notes, setNotes] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when edit data loads */
    setName(company?.name ?? "");
    setPhone(company?.phone ?? "");
    setEmail(company?.email ?? "");
    setWebsite(company?.website ?? "");
    setContactPersonName(company?.contactPersonName ?? "");
    setContactPersonPhone(company?.contactPersonPhone ?? "");
    setContactPersonEmail(company?.contactPersonEmail ?? "");
    setNotes(company?.notes ?? "");
    setIsActive(company?.isActive ?? true);
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [company]);

  const validationKey = useMemo(
    () => validateCompanyForm(name, email, contactPersonEmail, website),
    [name, email, contactPersonEmail, website],
  );

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name: name.trim(),
        phone: phone || null,
        email: email || null,
        website: website || null,
        contactPersonName: contactPersonName || null,
        contactPersonPhone: contactPersonPhone || null,
        contactPersonEmail: contactPersonEmail || null,
        notes: notes || null,
        isActive,
      };
      if (mode === "edit" && company) {
        await updateLicenseCompany(company.id, payload);
      } else {
        await createLicenseCompany(payload);
      }
    },
    onSuccess: () => {
      onSaved();
    },
    onError: (error) => {
      setErrorMessage(
        getLicenseManagementApiErrorMessage(
          error,
          t,
          "licenseManagement:messages.operationFailed",
        ),
      );
    },
  });

  return (
    <div className="space-y-4">
      {errorMessage ? <FormError message={errorMessage} /> : null}
      {validationKey ? (
        <FormError message={t(`licenseManagement:messages.${validationKey}`)} />
      ) : null}
      <div className="grid gap-4 md:grid-cols-2">
        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="company-name">{t("licenseManagement:form.companyName")}</Label>
          <Input id="company-name" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="company-email">{t("licenseManagement:table.email")}</Label>
          <Input id="company-email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="company-phone">{t("licenseManagement:table.phone")}</Label>
          <Input id="company-phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="company-website">{t("licenseManagement:form.website")}</Label>
          <Input id="company-website" value={website} onChange={(e) => setWebsite(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="company-contact-name">{t("licenseManagement:form.contactPersonName")}</Label>
          <Input id="company-contact-name" value={contactPersonName} onChange={(e) => setContactPersonName(e.target.value)} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="company-contact-phone">{t("licenseManagement:form.contactPersonPhone")}</Label>
          <Input id="company-contact-phone" value={contactPersonPhone} onChange={(e) => setContactPersonPhone(e.target.value)} />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="company-contact-email">{t("licenseManagement:form.contactPersonEmail")}</Label>
          <Input id="company-contact-email" value={contactPersonEmail} onChange={(e) => setContactPersonEmail(e.target.value)} />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="company-notes">{t("licenseManagement:form.notes")}</Label>
          <Textarea id="company-notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
        </div>
        <CheckboxField
          id="company-active"
          label={t("common:status.active")}
          checked={isActive}
          onCheckedChange={(checked) => setIsActive(checked === true)}
        />
      </div>
      <div className="flex flex-wrap justify-end gap-2">
        <Button type="button" variant="outline" onClick={onCancel} disabled={mutation.isPending}>
          {t("common:actions.cancel")}
        </Button>
        <Button
          type="button"
          disabled={Boolean(validationKey) || mutation.isPending}
          onClick={() => mutation.mutate()}
        >
          {t("common:actions.save")}
        </Button>
      </div>
    </div>
  );
}
