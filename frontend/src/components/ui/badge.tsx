import * as React from "react"

import { badgeVariants, type BadgeVariants } from "@/components/ui/badge-variants"
import { cn } from "@/lib/utils"

function Badge({
  className,
  variant,
  ...props
}: React.ComponentProps<"span"> & BadgeVariants) {
  return (
    <span
      data-slot="badge"
      className={cn(badgeVariants({ variant }), className)}
      {...props}
    />
  )
}

export { Badge }
