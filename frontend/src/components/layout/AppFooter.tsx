export function AppFooter() {
  return (
    <footer className="shrink-0 border-t bg-background/95 px-4 py-2 text-xs text-muted-foreground md:px-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span>© {new Date().getFullYear()} SAS Portal</span>
        <span>v2</span>
      </div>
    </footer>
  );
}
