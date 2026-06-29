import type { ColumnDef } from "@tanstack/react-table";
import type { TFunction } from "i18next";

import { DateTimeText } from "@/components/common/DateTimeText";
import { RowActions } from "@/components/common/RowActions";
import { StatusBadge } from "@/components/common/StatusBadge";
import type { DataTableColumnMeta } from "@/components/common/data-table";
import {
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import type {
  LicenseCompanyListItem,
  LicensedProductListItem,
  LicensePackageListItem,
  LicensePurchaseListItem,
} from "@/features/license-management/types";

type StatusToggleOptions<T> = {
  t: TFunction;
  canManage: boolean;
  isStatusPending: boolean;
  onDetail: (item: T) => void;
  onEdit: (item: T) => void;
  onToggleStatus: (item: T) => void;
};

type BaseOptions<T> = {
  t: TFunction;
  canManage: boolean;
  onDetail: (item: T) => void;
  onEdit: (item: T) => void;
};

export function createLicenseCompanyColumns({
  t,
  canManage,
  isStatusPending,
  onDetail,
  onEdit,
  onToggleStatus,
}: StatusToggleOptions<LicenseCompanyListItem>): ColumnDef<LicenseCompanyListItem, unknown>[] {
  return [
    {
      accessorKey: "name",
      header: () => t("licenseManagement:table.companyName"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "email",
      header: () => t("licenseManagement:table.email"),
      cell: ({ row }) => row.original.email ?? "-",
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "phone",
      header: () => t("licenseManagement:table.phone"),
      cell: ({ row }) => row.original.phone ?? "-",
    },
    {
      accessorKey: "supportEmail",
      header: () => t("licenseManagement:table.supportEmail"),
      cell: ({ row }) => row.original.supportEmail ?? "-",
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "contactPersonName",
      header: () => t("licenseManagement:table.contactPerson"),
      cell: ({ row }) => row.original.contactPersonName ?? "-",
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      id: "status",
      header: () => t("common:fields.status"),
      cell: ({ row }) => <StatusBadge isActive={row.original.isActive} />,
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuLabel>{t("common:actions.detail")}</DropdownMenuLabel>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("common:actions.detail")}
          </DropdownMenuItem>
          {canManage ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => onEdit(row.original)}>
                {t("common:actions.edit")}
              </DropdownMenuItem>
              <DropdownMenuItem
                disabled={isStatusPending}
                onClick={() => onToggleStatus(row.original)}
              >
                {row.original.isActive
                  ? t("licenseManagement:actions.deactivateCompany")
                  : t("licenseManagement:actions.activateCompany")}
              </DropdownMenuItem>
            </>
          ) : null}
        </RowActions>
      ),
    },
  ];
}

export function createLicenseProductColumns({
  t,
  canManage,
  isStatusPending,
  onDetail,
  onEdit,
  onToggleStatus,
  getLicenseTypeLabel,
}: StatusToggleOptions<LicensedProductListItem> & {
  getLicenseTypeLabel: (value: string | null) => string;
}): ColumnDef<LicensedProductListItem, unknown>[] {
  return [
    {
      accessorKey: "name",
      header: () => t("licenseManagement:table.productName"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "vendorCompanyName",
      header: () => t("licenseManagement:table.vendor"),
      cell: ({ row }) => row.original.vendorCompanyName ?? t("licenseManagement:form.noVendor"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "category",
      header: () => t("licenseManagement:table.category"),
      cell: ({ row }) => row.original.category ?? "-",
    },
    {
      id: "defaultLicenseType",
      header: () => t("licenseManagement:table.defaultLicenseType"),
      cell: ({ row }) =>
        row.original.defaultLicenseType
          ? getLicenseTypeLabel(row.original.defaultLicenseType)
          : "-",
    },
    {
      id: "status",
      header: () => t("common:fields.status"),
      cell: ({ row }) => <StatusBadge isActive={row.original.isActive} />,
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("common:actions.detail")}
          </DropdownMenuItem>
          {canManage ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => onEdit(row.original)}>
                {t("common:actions.edit")}
              </DropdownMenuItem>
              <DropdownMenuItem
                disabled={isStatusPending}
                onClick={() => onToggleStatus(row.original)}
              >
                {row.original.isActive
                  ? t("licenseManagement:actions.deactivateProduct")
                  : t("licenseManagement:actions.activateProduct")}
              </DropdownMenuItem>
            </>
          ) : null}
        </RowActions>
      ),
    },
  ];
}

export function createLicensePurchaseColumns({
  t,
  canManage,
  onDetail,
  onEdit,
  getPurchaseTypeLabel,
  getPurchaseStatusLabel,
}: BaseOptions<LicensePurchaseListItem> & {
  getPurchaseTypeLabel: (value: string) => string;
  getPurchaseStatusLabel: (value: string) => string;
}): ColumnDef<LicensePurchaseListItem, unknown>[] {
  return [
    {
      accessorKey: "title",
      header: () => t("licenseManagement:table.purchaseTitle"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      id: "purchaseType",
      header: () => t("licenseManagement:table.purchaseType"),
      cell: ({ row }) => getPurchaseTypeLabel(row.original.purchaseType),
    },
    {
      id: "purchaseDate",
      header: () => t("licenseManagement:table.purchaseDate"),
      cell: ({ row }) =>
        row.original.purchaseDate ? (
          <DateTimeText
            value={row.original.purchaseDate}
            options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
          />
        ) : (
          "-"
        ),
    },
    {
      accessorKey: "supplierCompanyName",
      header: () => t("licenseManagement:table.supplierCompany"),
      cell: ({ row }) => row.original.supplierCompanyName ?? "-",
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "supportCompanyName",
      header: () => t("licenseManagement:table.supportCompany"),
      cell: ({ row }) => row.original.supportCompanyName ?? "-",
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
    {
      accessorKey: "contractNumber",
      header: () => t("licenseManagement:table.contractNumber"),
      cell: ({ row }) => row.original.contractNumber ?? "-",
    },
    {
      id: "status",
      header: () => t("common:fields.status"),
      cell: ({ row }) => getPurchaseStatusLabel(row.original.status),
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("common:actions.detail")}
          </DropdownMenuItem>
          {canManage ? (
            <DropdownMenuItem onClick={() => onEdit(row.original)}>
              {t("common:actions.edit")}
            </DropdownMenuItem>
          ) : null}
        </RowActions>
      ),
    },
  ];
}

export function createLicensePackageColumns({
  t,
  canManage,
  onDetail,
  onEdit,
  getLicenseTypeLabel,
  getPackageStatusLabel,
  showPurchaseColumn = true,
}: BaseOptions<LicensePackageListItem> & {
  getLicenseTypeLabel: (value: string) => string;
  getPackageStatusLabel: (value: string) => string;
  showPurchaseColumn?: boolean;
}): ColumnDef<LicensePackageListItem, unknown>[] {
  const columns: ColumnDef<LicensePackageListItem, unknown>[] = [
    {
      accessorKey: "productName",
      header: () => t("licenseManagement:table.product"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
    },
  ];

  if (showPurchaseColumn) {
    columns.push({
      accessorKey: "purchaseTitle",
      header: () => t("licenseManagement:table.purchase"),
      meta: { truncate: true } satisfies DataTableColumnMeta,
    });
  }

  columns.push(
    {
      id: "licenseType",
      header: () => t("licenseManagement:table.licenseType"),
      cell: ({ row }) => getLicenseTypeLabel(row.original.licenseType),
    },
    {
      accessorKey: "quantity",
      header: () => t("licenseManagement:table.quantity"),
    },
    {
      accessorKey: "usedQuantity",
      header: () => t("licenseManagement:table.usedQuantity"),
    },
    {
      accessorKey: "availableQuantity",
      header: () => t("licenseManagement:table.availableQuantity"),
    },
    {
      id: "startDate",
      header: () => t("licenseManagement:table.startDate"),
      cell: ({ row }) =>
        row.original.startDate ? (
          <DateTimeText
            value={row.original.startDate}
            options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
          />
        ) : (
          "-"
        ),
    },
    {
      id: "endDate",
      header: () => t("licenseManagement:table.endDate"),
      cell: ({ row }) =>
        row.original.endDate ? (
          <DateTimeText
            value={row.original.endDate}
            options={{ year: "numeric", month: "2-digit", day: "2-digit" }}
          />
        ) : (
          "-"
        ),
    },
    {
      id: "isPerpetual",
      header: () => t("licenseManagement:table.isPerpetual"),
      cell: ({ row }) =>
        row.original.isPerpetual ? t("licenseManagement:boolean.yes") : t("licenseManagement:boolean.no"),
    },
    {
      id: "renewalRequired",
      header: () => t("licenseManagement:table.renewalRequired"),
      cell: ({ row }) =>
        row.original.renewalRequired ? t("licenseManagement:boolean.yes") : t("licenseManagement:boolean.no"),
    },
    {
      id: "status",
      header: () => t("common:fields.status"),
      cell: ({ row }) => getPackageStatusLabel(row.original.status),
    },
    {
      id: "actions",
      header: () => t("common:fields.actions"),
      meta: { isAction: true } satisfies DataTableColumnMeta,
      cell: ({ row }) => (
        <RowActions>
          <DropdownMenuItem onClick={() => onDetail(row.original)}>
            {t("common:actions.detail")}
          </DropdownMenuItem>
          {canManage ? (
            <DropdownMenuItem onClick={() => onEdit(row.original)}>
              {t("common:actions.edit")}
            </DropdownMenuItem>
          ) : null}
        </RowActions>
      ),
    },
  );

  return columns;
}
