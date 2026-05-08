import { RouterProvider } from "react-router-dom";
import { useEffect } from "react";

import { AppToaster } from "@/components/common/AppToaster";
import { router } from "@/app/router";
import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { resolveApiAssetUrl } from "@/lib/api-client";

const DEFAULT_FAVICON_HREF = "/favicon.svg";

const resolveFaviconType = (href: string | null): string => {
  if (!href) return "image/svg+xml";
  const lower = href.toLowerCase().split("?")[0];
  if (lower.endsWith(".png")) return "image/png";
  if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
  if (lower.endsWith(".svg")) return "image/svg+xml";
  return "image/svg+xml";
};

const applyFavicon = (href: string, type: string): void => {
  const head = document.head;
  if (!head) return;

  let link = head.querySelector<HTMLLinkElement>('link[rel~="icon"]');
  if (!link) {
    link = document.createElement("link");
    link.rel = "icon";
    head.appendChild(link);
  }

  if (link.getAttribute("href") !== href) {
    link.setAttribute("href", href);
  }

  if (link.getAttribute("type") !== type) {
    link.setAttribute("type", type);
  }
};

export function App() {
  const brandingQuery = useBrandingSettings();

  useEffect(() => {
    document.title = brandingQuery.data.browserTitle || "SAS Portal v2";
  }, [brandingQuery.data.browserTitle]);

  useEffect(() => {
    const rawFaviconUrl = brandingQuery.data.faviconUrl ?? DEFAULT_FAVICON_HREF;
    const resolvedHref = resolveApiAssetUrl(rawFaviconUrl) ?? DEFAULT_FAVICON_HREF;
    const type = resolveFaviconType(resolvedHref);
    applyFavicon(resolvedHref, type);
  }, [brandingQuery.data.faviconUrl]);

  return (
    <>
      <RouterProvider router={router} />
      <AppToaster />
    </>
  );
}
