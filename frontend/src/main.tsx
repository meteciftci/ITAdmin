import { QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import "@/app/i18n";
import { App } from "@/app/App";
import { queryClient } from "@/app/query-client";
import { ThemeProvider } from "@/components/theme/ThemeProvider";
import "@/index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <App />
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>,
);
