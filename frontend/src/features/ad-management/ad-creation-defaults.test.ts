import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, it } from "node:test";

import { buildUpdateAdManagementSettingsPayload } from "./ad-management-settings-payload.ts";
import {
  resolveAdGroupCreateTargetOu,
  resolveAdUserCreateTargetOu,
} from "./resolve-ad-create-target-ou.ts";
import type { AdManagementSettings } from "./types.ts";

const currentDir = dirname(fileURLToPath(import.meta.url));

function readSource(relativePath: string): string {
  return readFileSync(join(currentDir, relativePath), "utf8");
}

function createSettings(
  overrides: Partial<AdManagementSettings> = {},
): AdManagementSettings {
  return {
    isConfigured: true,
    isEnabled: true,
    domainFqdn: "corp.example.com",
    defaultUserCreationUpnSuffix: null,
    defaultUserOu: null,
    defaultGroupOu: null,
    defaultComputerOu: null,
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
    lastValidatedAt: null,
    lastValidationStatus: null,
    lastValidationMessage: null,
    notificationSettings: { rules: [] },
    ...overrides,
  };
}

describe("resolve-ad-create-target-ou", () => {
  it("prefers selected OU over default user OU and users root OU", () => {
    const result = resolveAdUserCreateTargetOu(
      "OU=Selected,DC=corp,DC=example,DC=com",
      {
        defaultUserOu: "OU=DefaultUsers,DC=corp,DC=example,DC=com",
        usersRootOu: "OU=Users,DC=corp,DC=example,DC=com",
      },
    );

    assert.equal(result, "OU=Selected,DC=corp,DC=example,DC=com");
  });

  it("uses defaultUserOu when no selection exists", () => {
    const result = resolveAdUserCreateTargetOu(null, {
      defaultUserOu: "OU=DefaultUsers,DC=corp,DC=example,DC=com",
      usersRootOu: "OU=Users,DC=corp,DC=example,DC=com",
    });

    assert.equal(result, "OU=DefaultUsers,DC=corp,DC=example,DC=com");
  });

  it("falls back to usersRootOu when defaultUserOu is missing", () => {
    const result = resolveAdUserCreateTargetOu(null, {
      defaultUserOu: null,
      usersRootOu: "OU=Users,DC=corp,DC=example,DC=com",
    });

    assert.equal(result, "OU=Users,DC=corp,DC=example,DC=com");
  });

  it("uses defaultGroupOu before groupsSearchBase", () => {
    const result = resolveAdGroupCreateTargetOu(null, {
      defaultGroupOu: "OU=DefaultGroups,DC=corp,DC=example,DC=com",
      groupsSearchBase: "OU=Groups,DC=corp,DC=example,DC=com",
    });

    assert.equal(result, "OU=DefaultGroups,DC=corp,DC=example,DC=com");
  });

  it("falls back to groupsSearchBase when defaultGroupOu is missing", () => {
    const result = resolveAdGroupCreateTargetOu(null, {
      defaultGroupOu: null,
      groupsSearchBase: "OU=Groups,DC=corp,DC=example,DC=com",
    });

    assert.equal(result, "OU=Groups,DC=corp,DC=example,DC=com");
  });
});

describe("ad creation defaults integration", () => {
  it("create user page uses resolveAdUserCreateTargetOu", () => {
    const source = readSource("AdCreateUserPage.tsx");
    assert.match(source, /resolveAdUserCreateTargetOu/);
    assert.doesNotMatch(source, /settingsQuery\.data\?\.usersRootOu/);
  });

  it("create group page uses resolveAdGroupCreateTargetOu", () => {
    const source = readSource("AdGroupCreatePage.tsx");
    assert.match(source, /resolveAdGroupCreateTargetOu/);
    assert.doesNotMatch(source, /settingsQuery\.data\?\.groupsSearchBase/);
  });

  it("creation defaults save payload includes default OU fields", () => {
    const settings = createSettings();
    const payload = buildUpdateAdManagementSettingsPayload(settings, {
      defaultUserOu: "OU=NewUsers,DC=corp,DC=example,DC=com",
      defaultGroupOu: "OU=NewGroups,DC=corp,DC=example,DC=com",
      defaultComputerOu: "OU=NewComputers,DC=corp,DC=example,DC=com",
      defaultUserCreationUpnSuffix: "corp.example.com",
    });

    assert.equal(payload.defaultUserOu, "OU=NewUsers,DC=corp,DC=example,DC=com");
    assert.equal(payload.defaultGroupOu, "OU=NewGroups,DC=corp,DC=example,DC=com");
    assert.equal(payload.defaultComputerOu, "OU=NewComputers,DC=corp,DC=example,DC=com");
    assert.equal(payload.defaultUserCreationUpnSuffix, "corp.example.com");
    assert.equal(payload.disabledUsersOu, settings.disabledUsersOu);
    assert.equal(payload.usersRootOu, settings.usersRootOu);
    assert.equal(payload.groupsSearchBase, settings.groupsSearchBase);
    assert.equal(payload.computersSearchBase, settings.computersSearchBase);
  });

  it("creation defaults tab does not render disabled users OU field", () => {
    const formSource = readSource("components/AdCreationDefaultsForm.tsx");
    const tabSource = readSource("AdManagementSettingsTab.tsx");

    assert.match(tabSource, /AdCreationDefaultsForm/);
    assert.match(tabSource, /creationDefaults/);
    assert.match(tabSource, /AdManagementScopesForm/);
    assert.match(tabSource, /scopes/);
    assert.doesNotMatch(formSource, /disabledUsersOu/i);
    assert.doesNotMatch(formSource, /disabledUsers/i);
  });

  it("connection form does not include OU scope text inputs", () => {
    const source = readSource("components/AdManagementConnectionForm.tsx");
    assert.doesNotMatch(source, /ad-mgmt-users-root-ou/);
    assert.doesNotMatch(source, /ad-mgmt-disabled-users-ou/);
    assert.doesNotMatch(source, /ad-mgmt-groups-search-base/);
    assert.doesNotMatch(source, /ad-mgmt-computers-search-base/);
    assert.doesNotMatch(source, /connection\.fields\.usersRootOu/);
  });
});
