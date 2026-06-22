import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  applyLdapConfigChange,
  buildCompleteSetupLdapPayload,
  buildCompleteSetupRequest,
  canAddAdminUser,
  createDefaultSetupFormValues,
  isAdManagementModuleValid,
  isOuSearchBelowMinLength,
  mapCompleteSetupFailureToast,
  shouldFetchAdminUserSearchResults,
  shouldFetchOuSearchResults,
  summaryContainsSecrets,
} from "./setup-form.ts";
import {
  canProceedFromWizardStep,
  getNextWizardStep,
  getPreviousWizardStep,
  SETUP_WIZARD_STEPS,
} from "./setup-wizard-state.ts";

const hints = {
  genericFallback: "generic",
  directoryUserNotFoundHint: "user-not-found",
  directoryUserProfileHint: "profile-not-loaded",
  ldapTimeoutHint: "timed-out",
};

const defaults = createDefaultSetupFormValues("Default LDAP");

describe("setup-form", () => {
  it("uses the provided default connection name", () => {
    assert.equal(createDefaultSetupFormValues("Varsayılan LDAP").ldap.name, "Varsayılan LDAP");
  });

  it("builds the LDAP payload without userSearchBase", () => {
    const payload = buildCompleteSetupLdapPayload({
      ...defaults.ldap,
      host: " dc01.test ",
      bindUserName: "bind",
    });

    assert.equal(payload.host, "dc01.test");
    assert.equal(payload.bindUserName, "bind");
    assert.equal("userSearchBase" in payload, false);
  });

  it("builds the complete-setup request with adminUsers and modules", () => {
    const request = buildCompleteSetupRequest({
      ...defaults,
      setupKey: "key",
      adminUsers: [{ userName: " admin ", displayName: "Admin User" }],
    });

    assert.equal(request.adminUsers.length, 1);
    assert.equal(request.adminUsers[0]?.userName, "admin");
    assert.equal(request.modules.adManagement?.isEnabled, false);
    assert.equal("userSearchBase" in request.ldap, false);
  });

  it("writes selected OU DN values into modules payload", () => {
    const request = buildCompleteSetupRequest({
      ...defaults,
      setupKey: "key",
      modules: {
        adManagement: {
          ...defaults.modules.adManagement,
          isEnabled: true,
          usersSearchBase: { distinguishedName: "OU=Users,DC=test,DC=local", label: "Users" },
          groupsSearchBase: { distinguishedName: "OU=Groups,DC=test,DC=local", label: "Groups" },
          computersSearchBase: { distinguishedName: "OU=Computers,DC=test,DC=local", label: "Computers" },
        },
      },
      adminUsers: [{ userName: "admin", displayName: "Admin" }],
    });

    assert.equal(request.modules.adManagement?.usersSearchBase, "OU=Users,DC=test,DC=local");
    assert.equal(request.modules.adManagement?.groupsSearchBase, "OU=Groups,DC=test,DC=local");
    assert.equal(request.modules.adManagement?.computersSearchBase, "OU=Computers,DC=test,DC=local");
  });

  it("writes disabled users OU into modules payload when selected", () => {
    const request = buildCompleteSetupRequest({
      ...defaults,
      setupKey: "key",
      modules: {
        adManagement: {
          ...defaults.modules.adManagement,
          isEnabled: true,
          usersSearchBase: { distinguishedName: "OU=Users,DC=test,DC=local", label: "Users" },
          groupsSearchBase: { distinguishedName: "OU=Groups,DC=test,DC=local", label: "Groups" },
          computersSearchBase: { distinguishedName: "OU=Computers,DC=test,DC=local", label: "Computers" },
          disabledUsersOu: { distinguishedName: "OU=Disabled,DC=test,DC=local", label: "Disabled Users" },
        },
      },
      adminUsers: [{ userName: "admin", displayName: "Admin" }],
    });

    assert.equal(request.modules.adManagement?.disabledUsersOu, "OU=Disabled,DC=test,DC=local");
  });

  it("allows AD Management setup without disabled users OU", () => {
    assert.equal(
      isAdManagementModuleValid({
        adManagement: {
          ...defaults.modules.adManagement,
          isEnabled: true,
          usersSearchBase: { distinguishedName: "OU=Users,DC=test,DC=local", label: "Users" },
          groupsSearchBase: { distinguishedName: "OU=Groups,DC=test,DC=local", label: "Groups" },
          computersSearchBase: { distinguishedName: "OU=Computers,DC=test,DC=local", label: "Computers" },
          disabledUsersOu: null,
        },
      }),
      true,
    );
  });

  it("does not require AD Management OU fields when disabled", () => {
    assert.equal(isAdManagementModuleValid(defaults.modules), true);
  });

  it("requires AD Management search bases when enabled", () => {
    assert.equal(
      isAdManagementModuleValid({
        adManagement: {
          ...defaults.modules.adManagement,
          isEnabled: true,
        },
      }),
      false,
    );
  });

  it("prevents duplicate admin user selection", () => {
    const existing = [{ userName: "admin", displayName: "Admin" }];
    assert.equal(
      canAddAdminUser(existing, { userName: "admin", displayName: "Admin Duplicate" }),
      false,
    );
  });

  it("falls back to the generic hint for an empty message", () => {
    assert.equal(mapCompleteSetupFailureToast("", hints), hints.genericFallback);
  });

  it("summary helper detects secret-like labels", () => {
    assert.equal(summaryContainsSecrets("host: dc01"), false);
    assert.equal(summaryContainsSecrets("setup key: hidden"), true);
    assert.equal(summaryContainsSecrets("bind password"), true);
  });

  it("clears LDAP-dependent OU selections and admin users when LDAP config changes", () => {
    const current = {
      ...defaults,
      setupKey: "key",
      modules: {
        adManagement: {
          ...defaults.modules.adManagement,
          isEnabled: true,
          usersSearchBase: { distinguishedName: "OU=Users,DC=test,DC=local", label: "Users" },
          groupsSearchBase: { distinguishedName: "OU=Groups,DC=test,DC=local", label: "Groups" },
          computersSearchBase: { distinguishedName: "OU=Computers,DC=test,DC=local", label: "Computers" },
          disabledUsersOu: { distinguishedName: "OU=Disabled,DC=test,DC=local", label: "Disabled Users" },
          defaultUserOu: { distinguishedName: "OU=NewUsers,DC=test,DC=local", label: "New Users" },
          defaultGroupOu: null,
          defaultComputerOu: null,
          deletedObjectsEnabled: true,
        },
      },
      adminUsers: [{ userName: "admin", displayName: "Admin User" }],
    };

    const next = applyLdapConfigChange(current, { ...current.ldap, host: "dc02.test" });

    assert.equal(next.ldap.host, "dc02.test");
    assert.equal(next.modules.adManagement.usersSearchBase, null);
    assert.equal(next.modules.adManagement.disabledUsersOu, null);
    assert.equal(next.modules.adManagement.defaultUserOu, null);
    assert.equal(next.modules.adManagement.isEnabled, true);
    assert.equal(next.modules.adManagement.deletedObjectsEnabled, true);
    assert.equal(next.adminUsers.length, 0);
  });

  it("does not trigger OU search when search is below minimum length", () => {
    assert.equal(isOuSearchBelowMinLength("a"), true);
    assert.equal(shouldFetchOuSearchResults(true, "a"), false);
    assert.equal(shouldFetchOuSearchResults(true, ""), true);
    assert.equal(shouldFetchOuSearchResults(true, "ou"), true);
  });

  it("does not trigger admin user search when LDAP is not validated", () => {
    assert.equal(shouldFetchAdminUserSearchResults(false, "admin"), false);
    assert.equal(shouldFetchAdminUserSearchResults(true, "a"), false);
    assert.equal(shouldFetchAdminUserSearchResults(true, "admin"), true);
  });
});

describe("setup-wizard-state", () => {
  it("navigates forward and backward across steps", () => {
    assert.equal(getNextWizardStep("setupKey"), "serverCheck");
    assert.equal(getPreviousWizardStep("serverCheck"), "setupKey");
    assert.equal(getNextWizardStep("summary"), null);
    assert.equal(getPreviousWizardStep("setupKey"), null);
    assert.equal(SETUP_WIZARD_STEPS.length, 6);
  });

  it("blocks server check next when canContinue is false", () => {
    const canProceed = canProceedFromWizardStep("serverCheck", {
      values: { ...defaults, setupKey: "key" },
      preflight: { checks: [], canContinue: false },
      ldapValidated: false,
    });

    assert.equal(canProceed, false);
  });

  it("requires ldap validation before modules step can proceed", () => {
    const canProceed = canProceedFromWizardStep("ldapConnection", {
      values: {
        ...defaults,
        setupKey: "key",
        ldap: {
          ...defaults.ldap,
          host: "dc01",
          baseDn: "DC=test,DC=local",
          bindUserName: "bind",
          bindPassword: "pw",
        },
      },
      preflight: { checks: [], canContinue: true },
      ldapValidated: false,
    });

    assert.equal(canProceed, false);
  });
});
