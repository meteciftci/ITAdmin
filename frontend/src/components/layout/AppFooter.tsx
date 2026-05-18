import { useBrandingSettings } from "@/hooks/useBrandingSettings";
import { getDefaultBrandingFooterText } from "@/lib/branding-footer";

export function AppFooter() {
  const { data: branding } = useBrandingSettings();
  const footerText = branding.footerText?.trim() || getDefaultBrandingFooterText();

  return (
    <footer className="shrink-0 border-t bg-background/95 px-4 py-2 text-xs text-muted-foreground md:px-6">
      <div className="flex justify-center text-center">
        <span className="break-words">{footerText}</span>
      </div>
    </footer>
  );
}
