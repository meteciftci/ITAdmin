import { RouterProvider } from "react-router-dom";

import { AppToaster } from "@/components/common/AppToaster";
import { router } from "@/app/router";

export function App() {
  return (
    <>
      <RouterProvider router={router} />
      <AppToaster />
    </>
  );
}
