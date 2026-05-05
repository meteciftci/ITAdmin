import { Link } from "react-router-dom";

import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export function NotFoundPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle>404 - Page not found</CardTitle>
        </CardHeader>
        <CardContent>
          <Link className={cn(buttonVariants())} to="/">
            Go to home
          </Link>
        </CardContent>
      </Card>
    </main>
  );
}
