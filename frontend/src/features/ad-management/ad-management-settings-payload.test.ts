import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildUpdateAdManagementSettingsPayload,
  NULLABLE_OU_SETTINGS_FIELDS,
  resolveNullableOverride,
} from "./ad-management-settings-payload.ts";
import type { AdManagementSettings } from "./types.ts";

const EXISTING_OU_DN = "OU=Existing,DC=corp,DC=example,DC=com";
const NEW_OU_DN = "OU=New,DC=corp,DC=example,DC=com";

function createSettings(
  overrides: Partial<AdManagementSettings> = {},
): AdManagementSettings {
  return {
    isConfigured: true,
    isEnabled: true,
    domainFqdn: "corp.example.com",
    defaultUserCreationUpnSuffix: "corp.example.com",
    defaultUserOu: EXISTING_OU_DN,
    defaultGroupOu: EXISTING_OU_DN,
    defaultComputerOu: EXISTING_OU_DN,
    netbiosDomainName: "CORP",
    defaultNamingContext: "DC=corp,DC=example,DC=com",
    baseDn: "DC=corp,DC=example,DC=com",
    usersRootOu: EXISTING_OU_DN,
    disabledUsersOu: EXISTING_OU_DN,
    groupsSearchBase: EXISTING_OU_DN,
    computersSearchBase: EXISTING_OU_DN,
    preferredDomainControllers: [],
    serviceAccountUserName: "svc_ad",
    hasServiceAccountPassword: true,
    powerShellHealthEnabled: false,
    powerShellTimeoutSeconds: 30,
    lastValidatedAt: null,
    lastValidationStatus: "Ok",
    lastValidationMessage: null,
    notificationSettings: { rules: [] },
    ...overrides,
  };
}

describe("resolveNullableOverride", () => {
  it("keeps current value when override is undefined", () => {
    assert.equal(resolveNullableOverride(undefined, EXISTING_OU_DN), EXISTING_OU_DN);
  });

  it("returns null when override is explicit null", () => {
    assert.equal(resolveNullableOverride(null, EXISTING_OU_DN), null);
  });

  it("returns new value when override is provided", () => {
    assert.equal(resolveNullableOverride(NEW_OU_DN, EXISTING_OU_DN), NEW_OU_DN);
  });
});

describe("buildUpdateAdManagementSettingsPayload nullable OU fields", () => {
  for (const field of NULLABLE_OU_SETTINGS_FIELDS) {
    it(`preserves existing ${field} when override is undefined`, () => {
      const settings = createSettings();
      const payload = buildUpdateAdManagementSettingsPayload(settings, {});

      assert.equal(payload[field], settings[field]);
    });

    it(`clears ${field} when override is null`, () => {
      const settings = createSettings();
      const payload = buildUpdateAdManagementSettingsPayload(settings, {
        [field]: null,
      });

      assert.equal(payload[field], null);
    });

    it(`updates ${field} when override is a new value`, () => {
      const settings = createSettings();
      const payload = buildUpdateAdManagementSettingsPayload(settings, {
        [field]: NEW_OU_DN,
      });

      assert.equal(payload[field], NEW_OU_DN);
    });
  }

  it("preserves scope OU fields when creation defaults override is partial", () => {
    const settings = createSettings();
    const payload = buildUpdateAdManagementSettingsPayload(settings, {
      defaultUserOu: NEW_OU_DN,
    });

    assert.equal(payload.defaultUserOu, NEW_OU_DN);
    assert.equal(payload.usersRootOu, settings.usersRootOu);
    assert.equal(payload.groupsSearchBase, settings.groupsSearchBase);
  });

  it("clears scope OU field when scopes form passes explicit null", () => {
    const settings = createSettings();
    const payload = buildUpdateAdManagementSettingsPayload(settings, {
      usersRootOu: null,
      disabledUsersOu: null,
      groupsSearchBase: null,
      computersSearchBase: null,
    });

    assert.equal(payload.usersRootOu, null);
    assert.equal(payload.disabledUsersOu, null);
    assert.equal(payload.groupsSearchBase, null);
    assert.equal(payload.computersSearchBase, null);
    assert.equal(payload.defaultUserOu, settings.defaultUserOu);
  });
});

describe("buildUpdateAdManagementSettingsPayload regression", () => {
  it("preserves non-OU fields when only OU overrides are provided", () => {
    const settings = createSettings();
    const payload = buildUpdateAdManagementSettingsPayload(settings, {
      usersRootOu: NEW_OU_DN,
    });

    assert.equal(payload.isEnabled, settings.isEnabled);
    assert.equal(payload.domainFqdn, settings.domainFqdn);
    assert.equal(payload.preferredDomainControllers, settings.preferredDomainControllers);
    assert.equal(payload.notificationSettings, settings.notificationSettings);
  });

  it("preserves existing OU values when connection tab saves partial overrides", () => {
    const settings = createSettings();
    const payload = buildUpdateAdManagementSettingsPayload(settings, {
      domainFqdn: "new.corp.example.com",
    });

    assert.equal(payload.domainFqdn, "new.corp.example.com");
    assert.equal(payload.usersRootOu, settings.usersRootOu);
    assert.equal(payload.defaultUserOu, settings.defaultUserOu);
  });
});
