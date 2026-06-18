import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  buildCompleteSetupLdapPayload,
  buildCompleteSetupRequest,
  createDefaultSetupFormValues,
  mapCompleteSetupFailureToast,
} from "./setup-form.ts";

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

  it("builds the LDAP payload from trimmed form values", () => {
    const payload = buildCompleteSetupLdapPayload({
      ...defaults.ldap,
      host: " dc01.test ",
      bindUserName: "bind",
    });

    assert.equal(payload.host, "dc01.test");
    assert.equal(payload.bindUserName, "bind");
    assert.equal("port" in payload, false);
    assert.equal("useSsl" in payload, false);
  });

  it("builds the complete-setup request with adminUsers and modules", () => {
    const request = buildCompleteSetupRequest({
      ...defaults,
      setupKey: "key",
      admin: { userName: " admin " },
    });

    assert.equal(request.adminUsers.length, 1);
    assert.equal(request.adminUsers[0]?.userName, "admin");
    assert.equal(request.modules.adManagement?.isEnabled, false);
    assert.equal("port" in request.ldap, false);
    assert.equal("useSsl" in request.ldap, false);
    assert.equal("nationalIdAttribute" in request.ldap, false);
  });

  it("sends the trimmed connection name without a hard-coded fallback", () => {
    const payload = buildCompleteSetupLdapPayload({ ...defaults.ldap, name: "   " });
    assert.equal(payload.name, "");
  });

  it("normalizes optional fields to null", () => {
    const payload = buildCompleteSetupLdapPayload(
      { ...defaults.ldap, bindUserDomain: "   " },
    );

    assert.equal(payload.bindUserDomain, null);
    assert.equal("nationalIdAttribute" in payload, false);
  });

  it("falls back to the generic hint for an empty message", () => {
    assert.equal(mapCompleteSetupFailureToast("", hints), hints.genericFallback);
  });

  it("returns the trimmed backend message when it is unrecognized", () => {
    assert.equal(mapCompleteSetupFailureToast("  custom error  ", hints), "custom error");
  });
});
