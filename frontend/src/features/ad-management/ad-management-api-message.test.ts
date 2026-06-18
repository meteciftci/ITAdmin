import assert from "node:assert/strict";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
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

function collectFeatureSourceFiles(directory: string): string[] {
  const files: string[] = [];

  for (const entry of readdirSync(directory)) {
    const fullPath = join(directory, entry);
    const stats = statSync(fullPath);
    if (stats.isDirectory()) {
      files.push(...collectFeatureSourceFiles(fullPath));
      continue;
    }

    if (fullPath.endsWith(".ts") || fullPath.endsWith(".tsx")) {
      files.push(fullPath);
    }
  }

  return files;
}

function extractBackendApiMessageKeys(): string[] {
  const keysFile = new URL(
    "../../../../backend/src/ITAdmin.Application/Common/Constants/AdManagementApiMessageKeys.cs",
    import.meta.url,
  );
  const source = readFileSync(keysFile, "utf8");
  const matches = source.matchAll(/Prefix \+ "([^"]+)"/g);

  return [...matches].map((match) => `apiMessages.${match[1]}`);
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

  it("ignores backend message field and uses fallback key when messageKey is missing", () => {
    const message = resolveAdManagementApiMessage(
      t,
      { messageKey: "", messageParams: null } as { messageKey: string },
      "adManagement:users.create.messages.created",
    );

    assert.equal(message, "Fallback created message");
  });

  it("uses fallback key when source has no messageKey", () => {
    const message = resolveAdManagementApiMessage(
      t,
      { messageKey: "" },
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

describe("AD management API message locale keys", () => {
  const trRoot = JSON.parse(
    readFileSync(new URL("../../locales/tr/adManagement.json", import.meta.url), "utf8"),
  ) as Record<string, unknown>;
  const enRoot = JSON.parse(
    readFileSync(new URL("../../locales/en/adManagement.json", import.meta.url), "utf8"),
  ) as Record<string, unknown>;
  const tr = trRoot.adManagement as Record<string, unknown>;
  const en = enRoot.adManagement as Record<string, unknown>;

  const backendKeys = extractBackendApiMessageKeys();

  for (const key of backendKeys) {
    it(`TR locale contains ${key}`, () => {
      assert.ok(readLocaleApiMessage(tr, key), `Missing TR locale for ${key}`);
    });

    it(`EN locale contains ${key}`, () => {
      assert.ok(readLocaleApiMessage(en, key), `Missing EN locale for ${key}`);
    });
  }

  it("resolveAdManagementApiMessage resolves controller validation keys", () => {
    const t = createT({
      "apiMessages.computers.invalidComputerId": "Invalid computer identifier.",
      "apiMessages.computers.targetOuRequired": "Target OU selection is required.",
      "apiMessages.groups.groupDnRequired": "Group identifier is required.",
    });

    assert.equal(
      resolveAdManagementApiMessage(
        t,
        { messageKey: "apiMessages.computers.invalidComputerId" },
        "adManagement:computers.errors.notFound",
      ),
      "Invalid computer identifier.",
    );
    assert.equal(
      resolveAdManagementApiMessage(
        t,
        { messageKey: "apiMessages.computers.targetOuRequired" },
        "adManagement:computers.errors.notFound",
      ),
      "Target OU selection is required.",
    );
    assert.equal(
      resolveAdManagementApiMessage(
        t,
        { messageKey: "apiMessages.groups.groupDnRequired" },
        "adManagement:groups.errors.notFound",
      ),
      "Group identifier is required.",
    );
  });
  it("connection form resolves lastValidationMessage via locale keys", () => {
    const source = readFileSync(
      new URL("./components/AdManagementConnectionForm.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /resolveLastValidationMessage/);
    assert.match(source, /resolveAdManagementApiMessage/);
    assert.doesNotMatch(source, /settings\.lastValidationMessage\s*\?\?/);
  });
});

describe("ad-management API message locale parity", () => {
  const featureRoot = fileURLToPath(new URL(".", import.meta.url));
  const helperSource = readFileSync(new URL("./ad-management-api-message.ts", import.meta.url), "utf8");

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

  it("helper does not read backend message field", () => {
    assert.doesNotMatch(helperSource, /(?:source|fields)\.message(?!Key|Params)/);
  });

  it("feature sources do not use response.message for API toasts", () => {
    const files = collectFeatureSourceFiles(featureRoot).filter(
      (file) => !file.endsWith(".test.ts"),
    );

    for (const file of files) {
      const source = readFileSync(file, "utf8");
      assert.doesNotMatch(
        source,
        /response\.message\b/,
        `${file} must not use response.message`,
      );
    }
  });
});
