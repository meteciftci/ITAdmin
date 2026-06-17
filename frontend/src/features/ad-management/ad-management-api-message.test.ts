import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import type { TFunction } from "i18next";

import {
  getAdManagementApiErrorMessage,
  resolveAdManagementApiMessage,
} from "./ad-management-api-message.ts";

function createT(translations: Record<string, string>): TFunction {
  const t = ((key: string, params?: Record<string, unknown>) => {
    const normalizedKey = key.startsWith("adManagement:") ? key.slice("adManagement:".length) : key;
    const template = translations[normalizedKey];
    if (!template) {
      return key;
    }

    if (!params) {
      return template;
    }

    return Object.entries(params).reduce(
      (result, [name, value]) => result.replace(`{{${name}}}`, String(value)),
      template,
    );
  }) as TFunction;

  return t;
}

describe("resolveAdManagementApiMessage", () => {
  const t = createT({
    "apiMessages.users.createSuccess": "AD user was created.",
    "apiMessages.users.notFound": "User {{samAccountName}} was not found.",
    "users.create.messages.created": "Fallback created message",
  });

  it("resolves messageKey to adManagement namespace translation", () => {
    const message = resolveAdManagementApiMessage(
      t,
      { messageKey: "apiMessages.users.createSuccess" },
      "adManagement:users.create.messages.created",
    );

    assert.equal(message, "AD user was created.");
  });

  it("passes messageParams to i18n interpolation", () => {
    const message = resolveAdManagementApiMessage(
      t,
      {
        messageKey: "apiMessages.users.notFound",
        messageParams: { samAccountName: "mete.ciftci" },
      },
      "adManagement:users.errors.notFound",
    );

    assert.equal(message, "User mete.ciftci was not found.");
  });

  it("falls back to legacy message when messageKey is missing", () => {
    const message = resolveAdManagementApiMessage(
      t,
      { message: "Legacy backend message" },
      "adManagement:users.create.messages.created",
    );

    assert.equal(message, "Legacy backend message");
  });

  it("uses fallback key when source has no message fields", () => {
    const message = resolveAdManagementApiMessage(
      t,
      {},
      "adManagement:users.create.messages.created",
    );

    assert.equal(message, "Fallback created message");
  });
});

describe("getAdManagementApiErrorMessage", () => {
  const t = createT({
    "apiMessages.users.updateFailed": "AD user could not be updated.",
    "users.edit.messages.updateFailed": "Fallback update failed",
  });

  it("returns fallback when error is not an axios error", () => {
    const message = getAdManagementApiErrorMessage(
      new Error("boom"),
      t,
      "adManagement:users.edit.messages.updateFailed",
    );

    assert.equal(message, "Fallback update failed");
  });
});

describe("ad-management API message integration", () => {
  const adFeatureFiles = [
    "AdUsersPage.tsx",
    "AdComputersPage.tsx",
    "AdCreateUserPage.tsx",
    "AdDeletedObjectRestorePage.tsx",
    "AdMoveComputerOuPage.tsx",
    "components/AdComputerDetailHeaderActions.tsx",
    "components/ad-user-detail/AdUserDetailHeaderActions.tsx",
    "components/AdGroupMembersSection.tsx",
    "components/AdDeleteGroupConfirmDialog.tsx",
    "run-sequential-membership-add.ts",
    "ad-management-save-error.ts",
  ];

  for (const file of adFeatureFiles) {
    it(`${file} uses AD API message helpers`, () => {
      const source = readFileSync(new URL(`./${file}`, import.meta.url), "utf8");

      assert.match(
        source,
        /resolveAdManagementApiMessage|getAdManagementApiErrorMessage/,
      );
      assert.doesNotMatch(source, /getApiErrorMessage/);
    });
  }
});
