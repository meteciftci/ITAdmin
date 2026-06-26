import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { buildUpdateAdManagementSettingsPayload } from "./ad-management-settings-payload.ts";
import { isAdManagementConnectionReady } from "./is-ad-management-connection-ready.ts";
import type { AdManagementSettings } from "./types.ts";

function createSettings(
  overrides: Partial<AdManagementSettings> = {},
): AdManagementSettings {
  return {
    isConfigured: true,
    isEnabled: true,
    domainFqdn: "corp.example.com",
    defaultUserCreationUpnSuffix: null,
    defaultUserOu: "OU=DefaultUsers,DC=corp,DC=example,DC=com",
    defaultGroupOu: "OU=DefaultGroups,DC=corp,DC=example,DC=com",
    defaultComputerOu: "OU=DefaultComputers,DC=corp,DC=example,DC=com",
    netbiosDomainName: "CORP",
    defaultNamingContext: "DC=corp,DC=example,DC=com",
    baseDn: "DC=corp,DC=example,DC=com",
    usersRootOu: "OU=Users,DC=corp,DC=example,DC=com",
    disabledUsersOu: "OU=Disabled,DC=corp,DC=example,DC=com",
    groupsSearchBase: "OU=Groups,DC=corp,DC=example,DC=com",
    computersSearchBase: "OU=Computers,DC=corp,DC=example,DC=com",
    preferredDomainControllers: [],
    serviceAccountUserName: "svc_ad",
    hasServiceAccountPassword: true,
    powerShellHealthEnabled: false,
    powerShellTimeoutSeconds: 30,
    lastValidatedAt: "2026-01-01T00:00:00Z",
    lastValidationStatus: "Ok",
    lastValidationMessage: null,
    notificationSettings: { rules: [] },
    ...overrides,
  };
}

describe("isAdManagementConnectionReady", () => {
  it("returns false when module is disabled", () => {
    assert.equal(
      isAdManagementConnectionReady(createSettings({ isEnabled: false })),
      false,
    );
  });

  it("returns false when core fields are missing", () => {
    assert.equal(
      isAdManagementConnectionReady(createSettings({ domainFqdn: null })),
      false,
    );
  });

  it("returns false when last validation is not successful", () => {
    assert.equal(
      isAdManagementConnectionReady(createSettings({ lastValidationStatus: "Failed" })),
      false,
    );
  });

  it("returns true when enabled, core fields filled and validation succeeded", () => {
    assert.equal(isAdManagementConnectionReady(createSettings()), true);
  });
});

describe("connection save payload", () => {
  it("preserves existing OU and default values when connection tab saves", () => {
    const settings = createSettings();
    const payload = buildUpdateAdManagementSettingsPayload(settings, {
      domainFqdn: "new.corp.example.com",
      netbiosDomainName: "NEWCORP",
      defaultNamingContext: settings.defaultNamingContext,
      baseDn: settings.baseDn,
      serviceAccountUserName: settings.serviceAccountUserName,
      preferredDomainControllers: settings.preferredDomainControllers,
      powerShellHealthEnabled: settings.powerShellHealthEnabled,
      powerShellTimeoutSeconds: settings.powerShellTimeoutSeconds,
    });

    assert.equal(payload.domainFqdn, "new.corp.example.com");
    assert.equal(payload.usersRootOu, settings.usersRootOu);
    assert.equal(payload.disabledUsersOu, settings.disabledUsersOu);
    assert.equal(payload.groupsSearchBase, settings.groupsSearchBase);
    assert.equal(payload.computersSearchBase, settings.computersSearchBase);
    assert.equal(payload.defaultUserOu, settings.defaultUserOu);
    assert.equal(payload.defaultGroupOu, settings.defaultGroupOu);
    assert.equal(payload.defaultComputerOu, settings.defaultComputerOu);
  });
});
