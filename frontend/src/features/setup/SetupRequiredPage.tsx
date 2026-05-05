import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export function SetupRequiredPage() {
  return (
    <main className="min-h-screen bg-muted/30 p-4 md:p-8">
      <div className="mx-auto max-w-2xl">
        <Card>
          <CardHeader>
            <CardTitle>Initial setup is required</CardTitle>
          </CardHeader>
          <CardContent className="space-y-2 text-sm text-muted-foreground">
            <p>SAS Portal kurulumu henüz tamamlanmamis.</p>
            <p>
              Bu fazda setup wizard frontend tarafinda hazir degil. Lutfen
              kurulum adimlarini backend/yetkili ekip ile tamamlayin.
            </p>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
