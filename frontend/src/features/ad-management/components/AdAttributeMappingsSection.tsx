import { useTranslation } from "react-i18next";

import { Button } from "@/components/ui/button";
import type { AdAttributeMapping } from "@/features/ad-management/types";

type Props = {
  mappings: AdAttributeMapping[];
  readOnly: boolean;
  isLoading: boolean;
  onCreate: () => void;
  onEdit: (mapping: AdAttributeMapping) => void;
  onDelete: (mapping: AdAttributeMapping) => void;
};

export function AdAttributeMappingsSection({
  mappings,
  readOnly,
  isLoading,
  onCreate,
  onEdit,
  onDelete,
}: Props) {
  const { t } = useTranslation(["settings", "common"]);

  const isEmpty = !isLoading && mappings.length === 0;

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h3 className="text-sm font-semibold">
            {t("settings:adManagement.mappings.title")}
          </h3>
          <p className="text-xs text-muted-foreground">
            {t("settings:adManagement.mappings.description")}
          </p>
        </div>
        {!readOnly ? (
          <Button onClick={onCreate}>
            {t("settings:adManagement.mappings.actions.create")}
          </Button>
        ) : null}
      </div>

      {isLoading ? (
        <p className="rounded-md border border-dashed px-3 py-2 text-sm text-muted-foreground">
          {t("common:loading")}
        </p>
      ) : null}

      {isEmpty ? (
        <div className="rounded-md border border-dashed px-3 py-6 text-center text-sm text-muted-foreground">
          <p>{t("settings:adManagement.mappings.empty.title")}</p>
          <p className="text-xs">{t("settings:adManagement.mappings.empty.description")}</p>
        </div>
      ) : null}

      {!isLoading && mappings.length > 0 ? (
        <div className="overflow-x-auto rounded-md border">
          <table className="w-full text-sm">
            <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
              <tr>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.displayName")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.logicalField")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.attributeName")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.isEnabled")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.isEditable")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.isSensitive")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.validationType")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.maskingStrategy")}
                </th>
                <th className="px-3 py-2 text-left">
                  {t("settings:adManagement.mappings.table.sortOrder")}
                </th>
                <th className="px-3 py-2 text-right">
                  {t("settings:adManagement.mappings.table.actions")}
                </th>
              </tr>
            </thead>
            <tbody>
              {mappings.map((item) => (
                <tr key={item.id} className="border-t">
                  <td className="px-3 py-2 font-medium">{item.displayName}</td>
                  <td className="px-3 py-2">
                    <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                      {item.logicalField}
                    </code>
                  </td>
                  <td className="px-3 py-2">
                    <code className="rounded bg-muted px-1.5 py-0.5 text-xs">
                      {item.attributeName}
                    </code>
                  </td>
                  <td className="px-3 py-2">{formatBool(item.isEnabled, t)}</td>
                  <td className="px-3 py-2">{formatBool(item.isEditable, t)}</td>
                  <td className="px-3 py-2">{formatBool(item.isSensitive, t)}</td>
                  <td className="px-3 py-2">
                    {t(`settings:adManagement.mappings.validationTypes.${item.validationType}`, {
                      defaultValue: item.validationType,
                    })}
                  </td>
                  <td className="px-3 py-2">
                    {t(`settings:adManagement.mappings.maskingStrategies.${item.maskingStrategy}`, {
                      defaultValue: item.maskingStrategy,
                    })}
                  </td>
                  <td className="px-3 py-2">{item.sortOrder}</td>
                  <td className="px-3 py-2 text-right">
                    {!readOnly ? (
                      <div className="flex justify-end gap-2">
                        <Button variant="outline" onClick={() => onEdit(item)}>
                          {t("settings:adManagement.mappings.actions.edit")}
                        </Button>
                        <Button variant="destructive" onClick={() => onDelete(item)}>
                          {t("settings:adManagement.mappings.actions.delete")}
                        </Button>
                      </div>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </div>
  );
}

function formatBool(value: boolean, t: (key: string) => string): string {
  return value
    ? t("common:status.active")
    : t("common:status.passive");
}
