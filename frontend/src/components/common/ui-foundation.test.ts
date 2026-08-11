import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it } from "node:test";

const commonDir = dirname(fileURLToPath(import.meta.url));
const sourceRoot = join(commonDir, "../..");

function readSource(relativePath: string): string {
  return readFileSync(join(sourceRoot, relativePath), "utf8");
}

describe("core UI foundation", () => {
  it("keeps page widths intentional across representative screen types", () => {
    const pageContainer = readSource("components/common/PageContainer.tsx");
    const homePage = readSource("features/home/HomePage.tsx");
    const usersPage = readSource("features/users/UsersPage.tsx");
    const rolesPage = readSource("features/roles/RolesPage.tsx");
    const auditLogsPage = readSource("features/audit-logs/AuditLogsPage.tsx");
    const securityLogsPage = readSource("features/security-logs/SecurityLogsPage.tsx");
    const notificationOutboxPage = readSource(
      "features/notification-outbox/NotificationOutboxPage.tsx",
    );

    assert.match(pageContainer, /"fluid" \| "wide" \| "form" \| "reading"/);
    assert.match(homePage, /<PageContainer variant="wide">/);
    assert.match(usersPage, /<PageContainer variant="fluid">/);
    assert.match(rolesPage, /<PageContainer variant="fluid">/);
    assert.match(auditLogsPage, /<PageContainer variant="fluid">/);
    assert.match(securityLogsPage, /<PageContainer variant="fluid">/);
    assert.match(notificationOutboxPage, /<PageContainer variant="fluid">/);
  });

  it("keeps loading and empty states inside list table surfaces", () => {
    const dataTable = readSource("components/common/data-table.tsx");

    assert.match(dataTable, /emptyDescription\?: ReactNode/);
    assert.match(dataTable, /getVisibleLeafColumns\(\)\.length/);
    assert.match(dataTable, /role="status"/);
    assert.match(dataTable, /sticky right-0/);

    for (const relativePath of [
      "features/users/UsersPage.tsx",
      "features/roles/RolesPage.tsx",
      "features/audit-logs/AuditLogsPage.tsx",
      "features/security-logs/SecurityLogsPage.tsx",
      "features/notification-outbox/NotificationOutboxPage.tsx",
    ]) {
      const source = readSource(relativePath);

      assert.match(source, /<DataTable/);
      assert.match(source, /isLoading=\{/);
      assert.match(source, /emptyDescription=/);
      assert.doesNotMatch(source, /<LoadingState|<EmptyState/);
    }
  });

  it("exposes popover state and restores trigger focus for keyboard users", () => {
    const popover = readSource("components/ui/popover.tsx");

    assert.match(popover, /aria-haspopup="dialog"/);
    assert.match(popover, /aria-expanded=\{open\}/);
    assert.match(popover, /triggerRef\.current/);
    assert.match(popover, /\.focus\(\)/);
  });

  it("uses Base UI for modal focus, dismissal, and screen reader semantics", () => {
    const dialog = readSource("components/ui/dialog.tsx");

    assert.match(dialog, /@base-ui\/react\/dialog/);
    assert.match(dialog, /<BaseDialog\.Root/);
    assert.match(dialog, /<BaseDialog\.Backdrop/);
    assert.match(dialog, /<BaseDialog\.Popup/);
    assert.match(dialog, /<BaseDialog\.Title/);
    assert.match(dialog, /<BaseDialog\.Description/);
  });

  it("keeps security and delivery states understandable without color alone", () => {
    const securityBadge = readSource(
      "features/security-logs/SecuritySeverityBadge.tsx",
    );
    const deliveryBadges = readSource(
      "features/notification-outbox/NotificationDeliveryBadges.tsx",
    );
    const outboxColumns = readSource(
      "features/notification-outbox/notification-outbox-columns.tsx",
    );

    assert.match(securityBadge, /TriangleAlert/);
    assert.match(securityBadge, /CircleAlert/);
    assert.match(deliveryBadges, /CircleCheck/);
    assert.match(deliveryBadges, /Clock3/);
    assert.match(outboxColumns, /<RowActions/);
    assert.doesNotMatch(outboxColumns, /variant="destructive"/);
  });

  it("does not announce valid date values as unavailable", () => {
    const dateTimeText = readSource("components/common/DateTimeText.tsx");

    assert.match(dateTimeText, /hasValidDate/);
    assert.match(dateTimeText, /hasValidDate \? undefined/);
  });
});
