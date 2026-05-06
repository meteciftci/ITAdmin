import { Link, useNavigate } from "react-router-dom";

import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import { useTranslation } from "react-i18next";

export function NotFoundPage() {
  const { t } = useTranslation(["errors"]);
  const navigate = useNavigate();

  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>{t("notFound.title")}</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="mb-4 text-sm text-muted-foreground">{t("notFound.description")}</p>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              className={cn(buttonVariants({ variant: "outline" }))}
              onClick={() => navigate(-1)}
            >
              {t("goBack")}
            </button>
            <Link className={cn(buttonVariants())} to="/dashboard">
              {t("goDashboard")}
            </Link>
          </div>
        </CardContent>
      </Card>
    </main>
  );
}
