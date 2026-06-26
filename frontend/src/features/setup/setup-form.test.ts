import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  applyLdapConfigChange,
  buildCompleteSetupLdapPayload,
  buildCompleteSetupRequest,
  canAddAdminUser,
  createDefaultSetupFormValues,
  mapCompleteSetupFailureToast,
  shouldFetchAdminUserSearchResults,
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

  it("builds the complete-setup request without modules payload", () => {
    const request = buildCompleteSetupRequest({
      ...defaults,
      setupKey: "key",
      adminUsers: [{ userName: " admin ", displayName: "Admin User" }],
    });

    assert.equal(request.adminUsers.length, 1);
    assert.equal(request.adminUsers[0]?.userName, "admin");
    assert.equal("modules" in request, false);
    assert.equal("userSearchBase" in request.ldap, false);
  });

  it("prevents duplicate admin user selection", () => {
    assert.equal(
      canAddAdminUser(
        [{ userName: "admin", displayName: "Admin" }],
        { userName: "ADMIN", displayName: "Admin Duplicate" },
      ),
      false,
    );
  });

  it("falls back to the generic hint for an empty message", () => {
    assert.equal(mapCompleteSetupFailureToast(undefined, hints), "generic");
  });

  it("summary helper detects secret-like labels", () => {
    assert.equal(summaryContainsSecrets("Setup key is configured"), true);
    assert.equal(summaryContainsSecrets("LDAP host configured"), false);
  });

  it("clears admin users when LDAP config changes", () => {
    const next = applyLdapConfigChange(
      {
        ...defaults,
        adminUsers: [{ userName: "admin", displayName: "Admin" }],
      },
      { ...defaults.ldap, host: "new-host" },
    );

    assert.equal(next.adminUsers.length, 0);
    assert.equal(next.ldap.host, "new-host");
  });

  it("does not trigger admin user search when LDAP is not validated", () => {
    assert.equal(shouldFetchAdminUserSearchResults(false, "admin"), false);
    assert.equal(shouldFetchAdminUserSearchResults(true, "a"), false);
    assert.equal(shouldFetchAdminUserSearchResults(true, "admin"), true);
  });
});

describe("setup-wizard-state", () => {
  it("navigates forward and backward across five steps", () => {
    assert.equal(SETUP_WIZARD_STEPS.length, 5);
    assert.equal(getNextWizardStep("setupKey"), "serverCheck");
    assert.equal(getNextWizardStep("ldapConnection"), "adminUsers");
    assert.equal(getPreviousWizardStep("adminUsers"), "ldapConnection");
    assert.equal(getPreviousWizardStep("summary"), "adminUsers");
  });

  it("blocks server check next when canContinue is false", () => {
    const canProceed = canProceedFromWizardStep("serverCheck", {
      values: { ...defaults, setupKey: "key" },
      preflight: { canContinue: false },
      ldapValidated: false,
    });

    assert.equal(canProceed, false);
  });

  it("requires ldap validation before admin users step can proceed", () => {
    const canProceed = canProceedFromWizardStep("ldapConnection", {
      values: {
        ...defaults,
        setupKey: "key",
        ldap: { ...defaults.ldap, host: "dc", baseDn: "dc=test", bindUserName: "bind", bindPassword: "pw" },
      },
      preflight: { canContinue: true },
      ldapValidated: false,
    });

    assert.equal(canProceed, false);
  });
});
