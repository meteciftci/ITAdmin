import { Navigate, type RouteObject } from "react-router-dom";

import {
  LicenseCompaniesPage,
  LicenseCompanyCreatePage,
  LicenseCompanyDetailPage,
  LicenseCompanyEditPage,
  LicenseManagementOverviewPage,
  LicenseManagementRedirectPage,
  LicensePackageCreatePage,
  LicensePackageDetailPage,
  LicensePackageEditPage,
  LicensePackagesPage,
  LicenseProductCategoriesPage,
  LicenseProductCategoryCreatePage,
  LicenseProductCategoryDetailPage,
  LicenseProductCategoryEditPage,
  LicenseProductCreatePage,
  LicenseProductDetailPage,
  LicenseProductEditPage,
  LicenseProductsPage,
  LicensePurchaseCreatePage,
  LicensePurchaseDetailPage,
  LicensePurchaseEditPage,
  LicensePurchasesPage,
  LicenseRequestCreatePage,
  LicenseRequestDetailPage,
  LicenseRequestEditPage,
  LicenseRequestsPage,
} from "@/app/lazy-pages";
import { LazyRoute } from "@/app/route-helpers";
import { AppLayout } from "@/components/layout/AppLayout";
import { RequireAuth } from "@/features/auth/RequireAuth";
import { RequirePermission } from "@/features/auth/RequirePermission";
import { PermissionCodes } from "@/lib/permission-codes";

export const licenseManagementRoutes: RouteObject[] = [
  {
    path: "/license-management",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseManagementRedirectPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/overview",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseManagementOverviewPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/companies",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseCompaniesPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/products",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/categories",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductCategoriesPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/categories/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageCatalog}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductCategoryCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/categories/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageCatalog}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductCategoryEditPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/categories/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductCategoryDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/companies/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageCatalog}>
          <AppLayout>
            <LazyRoute>
              <LicenseCompanyCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/companies/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageCatalog}>
          <AppLayout>
            <LazyRoute>
              <LicenseCompanyEditPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/companies/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseCompanyDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/products/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageCatalog}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/products/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageCatalog}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductEditPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/products/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseProductDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/purchases/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManagePurchases}>
          <AppLayout>
            <LazyRoute>
              <LicensePurchaseCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/purchases/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManagePurchases}>
          <AppLayout>
            <LazyRoute>
              <LicensePurchaseEditPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/purchases/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicensePurchaseDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/packages/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManagePurchases}>
          <AppLayout>
            <LazyRoute>
              <LicensePackageCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/packages/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManagePurchases}>
          <AppLayout>
            <LazyRoute>
              <LicensePackageEditPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/packages/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicensePackageDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/acquisitions",
    element: <Navigate to="/license-management/purchases" replace />,
  },
  {
    path: "/license-management/purchases",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicensePurchasesPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/requests/create",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageRequests}>
          <AppLayout>
            <LazyRoute>
              <LicenseRequestCreatePage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/requests/:id/edit",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.ManageRequests}>
          <AppLayout>
            <LazyRoute>
              <LicenseRequestEditPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/requests/:id",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseRequestDetailPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/requests",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicenseRequestsPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
  {
    path: "/license-management/packages",
    element: (
      <RequireAuth>
        <RequirePermission permission={PermissionCodes.LicenseManagement.View}>
          <AppLayout>
            <LazyRoute>
              <LicensePackagesPage />
            </LazyRoute>
          </AppLayout>
        </RequirePermission>
      </RequireAuth>
    ),
  },
];
