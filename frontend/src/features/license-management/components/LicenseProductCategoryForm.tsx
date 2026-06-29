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
  createLicenseProductCategory,
  updateLicenseProductCategory,
} from "@/features/license-management/api";
import { validateCategoryForm } from "@/features/license-management/form-validation";
import type { LicenseProductCategoryDetail } from "@/features/license-management/types";
import { getLicenseManagementApiErrorMessage } from "@/features/license-management/license-api-error";

type Props = {
  mode: "create" | "edit";
  category?: LicenseProductCategoryDetail | null;
  onCancel: () => void;
  onSaved: () => void;
};

export function LicenseProductCategoryForm({ mode, category, onCancel, onSaved }: Props) {
  const { t } = useTranslation(["licenseManagement", "common"]);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    /* eslint-disable react-hooks/set-state-in-effect -- hydrate fields when edit data loads */
    setName(category?.name ?? "");
    setDescription(category?.description ?? "");
    setIsActive(category?.isActive ?? true);
    setErrorMessage(null);
    /* eslint-enable react-hooks/set-state-in-effect */
  }, [category]);

  const validationKey = useMemo(() => validateCategoryForm(name), [name]);

  const mutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name: name.trim(),
        description: description.trim() || null,
        isActive,
      };
      if (mode === "edit" && category) {
        await updateLicenseProductCategory(category.id, payload);
      } else {
        await createLicenseProductCategory(payload);
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
          <Label htmlFor="category-name">{t("licenseManagement:form.categoryName")}</Label>
          <Input id="category-name" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="space-y-2 md:col-span-2">
          <Label htmlFor="category-description">{t("licenseManagement:form.description")}</Label>
          <Textarea
            id="category-description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>
        <CheckboxField
          id="category-active"
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
