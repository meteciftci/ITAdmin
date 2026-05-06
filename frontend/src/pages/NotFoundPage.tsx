import { Link, useNavigate } from "react-router-dom";

import { SectionCard } from "@/components/common/SectionCard";
import { AppLayout } from "@/components/layout/AppLayout";
import { Button, buttonVariants } from "@/components/ui/button";
import { useAuthStore } from "@/features/auth/auth-store";
import { useTranslation } from "react-i18next";

export function NotFoundPage() {
  const { t } = useTranslation(["errors", "common"]);
  const navigate = useNavigate();
  const accessToken = useAuthStore((state) => state.accessToken);

  const content = (
    <main className="mx-auto flex w-full max-w-2xl items-center justify-center p-4 md:p-6">
      <SectionCard
        title={t("errors:notFound.title")}
        description={t("errors:notFound.description")}
      >
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => navigate(-1)}>
            {t("common:actions.back")}
          </Button>
          <Link className={buttonVariants()} to="/dashboard">
            {t("common:actions.goToDashboard")}
          </Link>
        </div>
      </SectionCard>
    </main>
  );

  if (accessToken) {
    return <AppLayout>{content}</AppLayout>;
  }

  return <div className="min-h-screen bg-muted/30">{content}</div>;
}
