import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildCompleteSetupLdapPayload,
  buildCompleteSetupRequest,
  canAddAdminUser,
  createDefaultSetupFormValues,
  isAdManagementModuleValid,
  mapCompleteSetupFailureToast,
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
