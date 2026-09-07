 
import { lazy } from "react";

export const HomePage = lazy(() =>
  import("@/features/home/HomePage").then((module) => ({ default: module.HomePage })),
);
export const AuditLogsPage = lazy(() =>
  import("@/features/audit-logs/AuditLogsPage").then((module) => ({
    default: module.AuditLogsPage,
  })),
);
export const SecurityLogsPage = lazy(() =>
  import("@/features/security-logs/SecurityLogsPage").then((module) => ({
    default: module.SecurityLogsPage,
  })),
);
export const PermissionsPage = lazy(() =>
  import("@/features/permissions/PermissionsPage").then((module) => ({
    default: module.PermissionsPage,
  })),
);
export const RolesPage = lazy(() =>
  import("@/features/roles/RolesPage").then((module) => ({ default: module.RolesPage })),
);
export const UsersPage = lazy(() =>
  import("@/features/users/UsersPage").then((module) => ({ default: module.UsersPage })),
);
export const SettingsRedirectPage = lazy(() =>
  import("@/features/settings/SettingsRedirectPage").then((module) => ({
    default: module.SettingsRedirectPage,
  })),
);
export const SystemUpdatesPage = lazy(() =>
  import("@/features/system-updates/SystemUpdatesPage").then((module) => ({
    default: module.SystemUpdatesPage,
  })),
);
export const ApplicationSettingsPage = lazy(() =>
  import("@/features/settings/ApplicationSettingsPage").then((module) => ({
    default: module.ApplicationSettingsPage,
  })),
);
export const ModuleSettingsPage = lazy(() =>
  import("@/features/settings/ModuleSettingsPage").then((module) => ({
    default: module.ModuleSettingsPage,
  })),
);
export const NotificationOutboxPage = lazy(() =>
  import("@/features/notification-outbox/NotificationOutboxPage").then((module) => ({
    default: module.NotificationOutboxPage,
  })),
);
export const NotificationSettingsRedirectPage = lazy(() =>
  import("@/features/notification-settings/NotificationSettingsRedirectPage").then((module) => ({
    default: module.NotificationSettingsRedirectPage,
  })),
);
export const NotificationSettingsProvidersPage = lazy(() =>
  import("@/features/notification-settings/NotificationSettingsPage").then((module) => ({
    default: module.NotificationSettingsPage,
  })),
);
export const NotificationSettingsTemplatesPage = lazy(() =>
  import("@/features/notification-settings/NotificationSettingsPage").then((module) => ({
    default: module.NotificationSettingsPage,
  })),
);
export const NotificationTemplateFormPage = lazy(() =>
  import("@/features/notification-settings/NotificationTemplateFormPage").then((module) => ({
    default: module.NotificationTemplateFormPage,
  })),
);
export const AdManagementSettingsPage = lazy(() =>
  import("@/features/ad-management/AdManagementSettingsPage").then((module) => ({
    default: module.AdManagementSettingsPage,
  })),
);
export const AdUsersPage = lazy(() =>
  import("@/features/ad-management/AdUsersPage").then((module) => ({
    default: module.AdUsersPage,
  })),
);
export const AdCreateUserPage = lazy(() =>
  import("@/features/ad-management/AdCreateUserPage").then((module) => ({
    default: module.AdCreateUserPage,
  })),
);
export const AdUserGroupsPage = lazy(() =>
  import("@/features/ad-management/AdUserGroupsPage").then((module) => ({
    default: module.AdUserGroupsPage,
  })),
);
export const AdUserDetailPage = lazy(() =>
  import("@/features/ad-management/AdUserDetailPage").then((module) => ({
    default: module.AdUserDetailPage,
  })),
);
export const AdEditUserPage = lazy(() =>
  import("@/features/ad-management/AdEditUserPage").then((module) => ({
    default: module.AdEditUserPage,
  })),
);
export const AdMoveUserOuPage = lazy(() =>
  import("@/features/ad-management/AdMoveUserOuPage").then((module) => ({
    default: module.AdMoveUserOuPage,
  })),
);
export const AdOperationLogsPage = lazy(() =>
  import("@/features/ad-management/AdOperationLogsPage").then((module) => ({
    default: module.AdOperationLogsPage,
  })),
);
export const AdGroupsPage = lazy(() =>
  import("@/features/ad-management/AdGroupsPage").then((module) => ({
    default: module.AdGroupsPage,
  })),
);
export const AdGroupDetailPage = lazy(() =>
  import("@/features/ad-management/AdGroupDetailPage").then((module) => ({
    default: module.AdGroupDetailPage,
  })),
);
export const AdGroupCreatePage = lazy(() =>
  import("@/features/ad-management/AdGroupCreatePage").then((module) => ({
    default: module.AdGroupCreatePage,
  })),
);
export const AdEditGroupPage = lazy(() =>
  import("@/features/ad-management/AdEditGroupPage").then((module) => ({
    default: module.AdEditGroupPage,
  })),
);
export const AdMoveGroupOuPage = lazy(() =>
  import("@/features/ad-management/AdMoveGroupOuPage").then((module) => ({
    default: module.AdMoveGroupOuPage,
  })),
);
export const AdComputersPage = lazy(() =>
  import("@/features/ad-management/AdComputersPage").then((module) => ({
    default: module.AdComputersPage,
  })),
);
export const AdComputerDetailPage = lazy(() =>
  import("@/features/ad-management/AdComputerDetailPage").then((module) => ({
    default: module.AdComputerDetailPage,
  })),
);
export const AdMoveComputerOuPage = lazy(() =>
  import("@/features/ad-management/AdMoveComputerOuPage").then((module) => ({
    default: module.AdMoveComputerOuPage,
  })),
);
export const AdComputerGroupsPage = lazy(() =>
  import("@/features/ad-management/AdComputerGroupsPage").then((module) => ({
    default: module.AdComputerGroupsPage,
  })),
);
export const AdOrganizationalUnitsPage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitsPage").then((module) => ({
    default: module.AdOrganizationalUnitsPage,
  })),
);
export const AdOrganizationalUnitDetailPage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitDetailPage").then((module) => ({
    default: module.AdOrganizationalUnitDetailPage,
  })),
);
export const AdOrganizationalUnitCreatePage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitCreatePage").then((module) => ({
    default: module.AdOrganizationalUnitCreatePage,
  })),
);
export const AdOrganizationalUnitRenamePage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitRenamePage").then((module) => ({
    default: module.AdOrganizationalUnitRenamePage,
  })),
);
export const AdOrganizationalUnitMovePage = lazy(() =>
  import("@/features/ad-management/AdOrganizationalUnitMovePage").then((module) => ({
    default: module.AdOrganizationalUnitMovePage,
  })),
);
export const AdDeletedObjectsPage = lazy(() =>
  import("@/features/ad-management/AdDeletedObjectsPage").then((module) => ({
    default: module.AdDeletedObjectsPage,
  })),
);
export const AdDeletedObjectDetailPage = lazy(() =>
  import("@/features/ad-management/AdDeletedObjectDetailPage").then((module) => ({
    default: module.AdDeletedObjectDetailPage,
  })),
);
export const AdDeletedObjectRestorePage = lazy(() =>
  import("@/features/ad-management/AdDeletedObjectRestorePage").then((module) => ({
    default: module.AdDeletedObjectRestorePage,
  })),
);
export const LicenseManagementRedirectPage = lazy(() =>
  import("@/features/license-management/LicenseManagementOverviewPage").then((module) => ({
    default: module.LicenseManagementRedirectPage,
  })),
);
export const LicenseManagementOverviewPage = lazy(() =>
  import("@/features/license-management/LicenseManagementOverviewPage").then((module) => ({
    default: module.LicenseManagementOverviewPage,
  })),
);
export const LicenseCompaniesPage = lazy(() =>
  import("@/features/license-management/LicenseCompaniesPage").then((module) => ({
    default: module.LicenseCompaniesPage,
  })),
);
export const LicenseProductsPage = lazy(() =>
  import("@/features/license-management/LicenseProductsPage").then((module) => ({
    default: module.LicenseProductsPage,
  })),
);
export const LicensePurchasesPage = lazy(() =>
  import("@/features/license-management/LicensePurchasesPage").then((module) => ({
    default: module.LicensePurchasesPage,
  })),
);
export const LicenseCompanyCreatePage = lazy(() =>
  import("@/features/license-management/LicenseCompanyCreatePage").then((module) => ({
    default: module.LicenseCompanyCreatePage,
  })),
);
export const LicenseCompanyEditPage = lazy(() =>
  import("@/features/license-management/LicenseCompanyEditPage").then((module) => ({
    default: module.LicenseCompanyEditPage,
  })),
);
export const LicenseCompanyDetailPage = lazy(() =>
  import("@/features/license-management/LicenseCompanyDetailPage").then((module) => ({
    default: module.LicenseCompanyDetailPage,
  })),
);
export const LicenseProductCreatePage = lazy(() =>
  import("@/features/license-management/LicenseProductCreatePage").then((module) => ({
    default: module.LicenseProductCreatePage,
  })),
);
export const LicenseProductEditPage = lazy(() =>
  import("@/features/license-management/LicenseProductEditPage").then((module) => ({
    default: module.LicenseProductEditPage,
  })),
);
export const LicenseProductDetailPage = lazy(() =>
  import("@/features/license-management/LicenseProductDetailPage").then((module) => ({
    default: module.LicenseProductDetailPage,
  })),
);
export const LicenseProductCategoriesPage = lazy(() =>
  import("@/features/license-management/LicenseProductCategoriesPage").then((module) => ({
    default: module.LicenseProductCategoriesPage,
  })),
);
export const LicenseProductCategoryCreatePage = lazy(() =>
  import("@/features/license-management/LicenseProductCategoryCreatePage").then((module) => ({
    default: module.LicenseProductCategoryCreatePage,
  })),
);
export const LicenseProductCategoryEditPage = lazy(() =>
  import("@/features/license-management/LicenseProductCategoryEditPage").then((module) => ({
    default: module.LicenseProductCategoryEditPage,
  })),
);
export const LicenseProductCategoryDetailPage = lazy(() =>
  import("@/features/license-management/LicenseProductCategoryDetailPage").then((module) => ({
    default: module.LicenseProductCategoryDetailPage,
  })),
);
export const LicensePurchaseCreatePage = lazy(() =>
  import("@/features/license-management/LicensePurchaseCreatePage").then((module) => ({
    default: module.LicensePurchaseCreatePage,
  })),
);
export const LicensePurchaseEditPage = lazy(() =>
  import("@/features/license-management/LicensePurchaseEditPage").then((module) => ({
    default: module.LicensePurchaseEditPage,
  })),
);
export const LicensePurchaseDetailPage = lazy(() =>
  import("@/features/license-management/LicensePurchaseDetailPage").then((module) => ({
    default: module.LicensePurchaseDetailPage,
  })),
);
export const LicensePackageCreatePage = lazy(() =>
  import("@/features/license-management/LicensePackageCreatePage").then((module) => ({
    default: module.LicensePackageCreatePage,
  })),
);
export const LicensePackageEditPage = lazy(() =>
  import("@/features/license-management/LicensePackageEditPage").then((module) => ({
    default: module.LicensePackageEditPage,
  })),
);
export const LicensePackageDetailPage = lazy(() =>
  import("@/features/license-management/LicensePackageDetailPage").then((module) => ({
    default: module.LicensePackageDetailPage,
  })),
);
export const LicenseManagementSettingsPage = lazy(() =>
  import("@/features/settings/LicenseManagementSettingsPage").then((module) => ({
    default: module.LicenseManagementSettingsPage,
  })),
);
export const LicensePackagesPage = lazy(() =>
  import("@/features/license-management/LicensePackagesPage").then((module) => ({
    default: module.LicensePackagesPage,
  })),
);
export const LicenseRequestsPage = lazy(() =>
  import("@/features/license-management/LicenseRequestsPage").then((module) => ({
    default: module.LicenseRequestsPage,
  })),
);
export const LicenseFulfillmentPage = lazy(() =>
  import("@/features/license-management/LicenseFulfillmentPage").then((module) => ({
    default: module.LicenseFulfillmentPage,
  })),
);
export const LicenseRequestCreatePage = lazy(() =>
  import("@/features/license-management/LicenseRequestCreatePage").then((module) => ({
    default: module.LicenseRequestCreatePage,
  })),
);
export const LicenseRequestEditPage = lazy(() =>
  import("@/features/license-management/LicenseRequestEditPage").then((module) => ({
    default: module.LicenseRequestEditPage,
  })),
);
export const LicenseRequestDetailPage = lazy(() =>
  import("@/features/license-management/LicenseRequestDetailPage").then((module) => ({
    default: module.LicenseRequestDetailPage,
  })),
);
export const ErrorPage = lazy(() =>
  import("@/pages/ErrorPage").then((module) => ({ default: module.ErrorPage })),
);
export const NotFoundPage = lazy(() =>
  import("@/pages/NotFoundPage").then((module) => ({ default: module.NotFoundPage })),
);
