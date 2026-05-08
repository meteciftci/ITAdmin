import { RouterProvider } from "react-router-dom";
import { useEffect } from "react";

import { AppToaster } from "@/components/common/AppToaster";
import { router } from "@/app/router";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";

export function App() {
  const brandingQuery = useBrandingSettings();

  useEffect(() => {
    document.title = brandingQuery.data.browserTitle || "SAS Portal v2";
  }, [brandingQuery.data.browserTitle]);

  return (
    <>
      <RouterProvider router={router} />
      <AppToaster />
    </>
  );
}
