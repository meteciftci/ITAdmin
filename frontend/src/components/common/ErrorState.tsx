import type { ReactNode } from "react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

type ErrorStateProps = {
  title: ReactNode;
  description?: ReactNode;
  retry?: ReactNode;
};

export function ErrorState({ title, description, retry }: ErrorStateProps) {
  return (
    <Alert variant="destructive">
      <AlertTitle>{title}</AlertTitle>
      {description ? <AlertDescription>{description}</AlertDescription> : null}
      {retry ? <div className="mt-3">{retry}</div> : null}
    </Alert>
  );
}
