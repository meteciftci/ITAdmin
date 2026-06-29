import { useQuery } from "@tanstack/react-query";
import { Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";

import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { getLicenseManagementOverview } from "@/features/license-management/api";
import { createApiErrorRouteState, getErrorRoutePath } from "@/lib/route-error";

export function LicenseManagementOverviewPage() {
  const { t } = useTranslation(["licenseManagement"]);
  const overviewQuery = useQuery({
    queryKey: ["license-management", "overview"],
    queryFn: getLicenseManagementOverview,
  });

  if (overviewQuery.isError) {
    const routeState = createApiErrorRouteState(overviewQuery.error, {
      fromPath: "/license-management/overview",
      retryPath: "/license-management/overview",
      sourceLabel: t("licenseManagement:overview.title"),
    });
    return (
      <Navigate to={getErrorRoutePath(routeState.code)} replace state={routeState} />
    );
  }

  const summary = overviewQuery.data;

  return (
    <section className="space-y-4">
      <PageHeader
        title={t("licenseManagement:overview.title")}
        description={t("licenseManagement:overview.description")}
      />
      {overviewQuery.isLoading ? <LoadingState /> : null}
      {summary ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
          {[
            { label: t("licenseManagement:overview.companyCount"), value: summary.companyCount },
            { label: t("licenseManagement:overview.activeProductCount"), value: summary.activeProductCount },
            { label: t("licenseManagement:overview.purchaseCount"), value: summary.purchaseCount },
            { label: t("licenseManagement:overview.packageCount"), value: summary.packageCount },
            { label: t("licenseManagement:overview.totalLicenseQuantity"), value: summary.totalLicenseQuantity },
          ].map((card) => (
            <Card key={card.label}>
              <CardHeader className="pb-2">
                <CardTitle className="text-sm font-medium text-muted-foreground">{card.label}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-3xl font-semibold">{card.value}</p>
              </CardContent>
            </Card>
          ))}
        </div>
      ) : null}
    </section>
  );
}

export function LicenseManagementRedirectPage() {
  return <Navigate to="/license-management/overview" replace />;
}
