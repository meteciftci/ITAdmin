import { Link, Navigate, useLocation, useNavigate, useParams } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { AxiosError } from "axios";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

import { EmptyState } from "@/components/common/EmptyState";
import { ErrorState } from "@/components/common/ErrorState";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { SectionCard } from "@/components/common/SectionCard";
import { buttonVariants } from "@/components/ui/button-variants";
import { useAuthStore } from "@/features/auth/auth-store";
import { getLicenseRequestById } from "@/features/license-management/api";
import { LicenseRequestAdAccessGuard } from "@/features/license-management/components/LicenseRequestAdAccessGuard";
import { LicenseRequestForm } from "@/features/license-management/components/LicenseRequestForm";
import {
  buildLicenseRequestDetailPath,
  LICENSE_REQUESTS_LIST_PATH,
} from "@/features/license-management/license-request-paths";
import { resolveLicenseRequestReturnPath } from "@/features/license-management/license-request-return-path";
import { getApiErrorMessage } from "@/lib/api-error";
import { canAccess } from "@/lib/permissions";
import { PermissionCodes } from "@/lib/permission-codes";
import { cn } from "@/lib/utils";

export function LicenseRequestEditPage() {
  const { t } = useTranslation(["licenseManagement", "common", "errors"]);
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const queryClient = useQueryClient();
  const user = useAuthStore((state) => state.user);
  const canManage = canAccess(user, PermissionCodes.LicenseManagement.ManageRequests);

  const detailQuery = useQuery({
    queryKey: ["license-management", "requests", "detail", id],
    queryFn: () => getLicenseRequestById(id!),
    enabled: Boolean(id) && canManage,
  });

  const returnPath = id
    ? resolveLicenseRequestReturnPath(location.state, buildLicenseRequestDetailPath(id))
    : LICENSE_REQUESTS_LIST_PATH;

  if (!canManage) {
    return <Navigate to={LICENSE_REQUESTS_LIST_PATH} replace />;
  }

  if (!id) {
    return (
      <section className="mx-auto w-full max-w-7xl space-y-4">
        <EmptyState title={t("licenseManagement:pages.requests.detail.notFound")} />
      </section>
    );
  }

  const isNotFound =
    detailQuery.isError
    && detailQuery.error instanceof AxiosError
    && detailQuery.error.response?.status === 404;

  return (
    <section className="mx-auto w-full max-w-7xl space-y-4">
      <PageHeader
        title={t("licenseManagement:pages.requests.edit.title")}
        description={t("licenseManagement:pages.requests.edit.description")}
        actions={
          <Link to={returnPath} className={cn(buttonVariants({ variant: "outline" }))}>
            {t("common:actions.back")}
          </Link>
        }
      />
      {detailQuery.isLoading ? <LoadingState /> : null}
      {detailQuery.isError && !isNotFound ? (
        <ErrorState
          title={t("errors:generic.title")}
          description={getApiErrorMessage(detailQuery.error, t("errors:generic.description"))}
        />
      ) : null}
      {isNotFound ? <EmptyState title={t("licenseManagement:pages.requests.detail.notFound")} /> : null}
      {detailQuery.data ? (
        <SectionCard title={t("licenseManagement:pages.requests.edit.formTitle")}>
          <LicenseRequestAdAccessGuard>
            <LicenseRequestForm
              mode="edit"
              request={detailQuery.data}
              onCancel={() => navigate(returnPath)}
              onSaved={(requestId) => {
                queryClient.invalidateQueries({ queryKey: ["license-management", "requests"] });
                queryClient.invalidateQueries({ queryKey: ["license-management", "requests", "detail", requestId] });
                toast.success(t("licenseManagement:messages.requestUpdated"));
                navigate(returnPath);
              }}
            />
          </LicenseRequestAdAccessGuard>
        </SectionCard>
      ) : null}
    </section>
  );
}
