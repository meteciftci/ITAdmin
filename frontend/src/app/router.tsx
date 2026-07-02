import { createBrowserRouter } from "react-router-dom";

import { coreRoutes, notFoundRoute } from "@/app/routes/core-routes";
import { adManagementRoutes } from "@/app/routes/ad-management-routes";
import { licenseManagementRoutes } from "@/app/routes/license-management-routes";
import { settingsRoutes } from "@/app/routes/settings-routes";

export const router = createBrowserRouter([
  ...coreRoutes,
  ...settingsRoutes,
  ...adManagementRoutes,
  ...licenseManagementRoutes,
  notFoundRoute,
]);
