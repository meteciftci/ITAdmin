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

function readLocaleApiMessage(
  locale: Record<string, unknown>,
  dottedKey: string,
): string | undefined {
  const segments = dottedKey.split(".");
  let current: unknown = locale;

  for (const segment of segments) {
    if (!current || typeof current !== "object" || !(segment in current)) {
      return undefined;
    }

    current = (current as Record<string, unknown>)[segment];
  }

  return typeof current === "string" ? current : undefined;
}

describe("AD management API message locale keys", () => {
  const tr = JSON.parse(
    readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
  ) as Record<string, unknown>;
  const en = JSON.parse(
    readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
  ) as Record<string, unknown>;

  const requiredKeys = [
    "adManagement.apiMessages.computers.invalidComputerId",
    "adManagement.apiMessages.computers.targetOuRequired",
    "adManagement.apiMessages.groups.groupDnRequired",
    "adManagement.apiMessages.users.invalidUserId",
    "adManagement.apiMessages.groups.invalidGroupId",
    "adManagement.apiMessages.deletedObjects.notFound",
  ];

  for (const key of requiredKeys) {
    it(`TR locale contains ${key}`, () => {
      assert.ok(readLocaleApiMessage(tr, key));
    });

    it(`EN locale contains ${key}`, () => {
      assert.ok(readLocaleApiMessage(en, key));
    });
  }

  it("resolveAdManagementApiMessage still resolves controller validation keys", () => {
    const t = createT({
      "apiMessages.computers.invalidComputerId": "Invalid computer identifier.",
      "apiMessages.computers.targetOuRequired": "Target OU selection is required.",
      "apiMessages.groups.groupDnRequired": "Group identifier is required.",
    });

    assert.equal(
      resolveAdManagementApiMessage(
        t,
        { messageKey: "apiMessages.computers.invalidComputerId", message: "Legacy TR" },
        "adManagement:computers.errors.notFound",
      ),
      "Invalid computer identifier.",
    );
    assert.equal(
      resolveAdManagementApiMessage(
        t,
        { messageKey: "apiMessages.computers.targetOuRequired", message: "Legacy TR" },
        "adManagement:computers.errors.notFound",
      ),
      "Target OU selection is required.",
    );
    assert.equal(
      resolveAdManagementApiMessage(
        t,
        { messageKey: "apiMessages.groups.groupDnRequired", message: "Legacy TR" },
        "adManagement:groups.errors.notFound",
      ),
      "Group identifier is required.",
    );
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
