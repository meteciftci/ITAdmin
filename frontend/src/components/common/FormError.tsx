import { Alert, AlertDescription } from "@/components/ui/alert";

type FormErrorProps = {
  message?: string | null;
};

export function FormError({ message }: FormErrorProps) {
  if (!message) return null;
  return (
    <Alert variant="destructive">
      <AlertDescription>{message}</AlertDescription>
    </Alert>
  );
}
