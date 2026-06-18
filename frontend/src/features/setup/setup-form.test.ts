import assert from "node:assert/strict";
import { describe, it } from "node:test";

import {
  STANDARD_LDAPS_PORT,
  SETUP_SECURE_CONNECTION_REQUIRED_MESSAGE_KEY,
  buildCompleteSetupLdapPayload,
  buildCompleteSetupRequest,
  defaultSetupFormValues,
  mapCompleteSetupFailureToast,
} from "./setup-form.ts";

const hints = {
  genericFallback: "generic",
  secureConnectionRequiredHint: "ldaps-required",
  directoryUserNotFoundHint: "user-not-found",
  directoryUserProfileHint: "profile-not-loaded",
  ldapTimeoutHint: "timed-out",
};

describe("setup-form", () => {
  it("defaults the port to the standard LDAPS port", () => {
    assert.equal(STANDARD_LDAPS_PORT, "636");
    assert.equal(defaultSetupFormValues.ldap.port, "636");
  });

  it("always sends useSsl true in the LDAP payload", () => {
    const payload = buildCompleteSetupLdapPayload(
      { ...defaultSetupFormValues.ldap, host: " dc01.test ", bindUserName: "bind" },
      636,
    );

    assert.equal(payload.useSsl, true);
    assert.equal(payload.host, "dc01.test");
    assert.equal(payload.port, 636);
  });

  it("always sends useSsl true in the complete-setup request", () => {
    const request = buildCompleteSetupRequest(
      {
        ...defaultSetupFormValues,
        setupKey: "key",
        admin: { userName: " admin ", password: "pw" },
      },
      636,
    );

    assert.equal(request.ldap.useSsl, true);
    assert.equal(request.admin.userName, "admin");
  });

  it("normalizes optional fields to null", () => {
    const payload = buildCompleteSetupLdapPayload(
      { ...defaultSetupFormValues.ldap, bindUserDomain: "   ", nationalIdAttribute: "" },
      636,
    );

    assert.equal(payload.bindUserDomain, null);
    assert.equal(payload.nationalIdAttribute, null);
  });

  it("maps the secure-connection-required message key to its hint", () => {
    const result = mapCompleteSetupFailureToast(
      SETUP_SECURE_CONNECTION_REQUIRED_MESSAGE_KEY,
      hints,
    );

    assert.equal(result, hints.secureConnectionRequiredHint);
  });

  it("falls back to the generic hint for an empty message", () => {
    assert.equal(mapCompleteSetupFailureToast("", hints), hints.genericFallback);
  });

  it("returns the trimmed backend message when it is unrecognized", () => {
    assert.equal(mapCompleteSetupFailureToast("  custom error  ", hints), "custom error");
  });
});
