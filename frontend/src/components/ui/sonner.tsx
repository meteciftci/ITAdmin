import { Toaster as Sonner } from "sonner";

type ToasterProps = React.ComponentProps<typeof Sonner>;

export function Toaster(props: ToasterProps) {
  return <Sonner closeButton richColors position="top-right" {...props} />;
}
