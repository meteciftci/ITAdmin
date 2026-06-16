import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

import {
  getAdOperationErrorSummary,
  parseAdOperationErrorMessage,
} from "./parse-ad-operation-error-message.ts";

const powerShellRestoreDiagnostic = {
  code: "AD_OPERATION_FAILED",
  operation: "DeletedObjectRestore",
  step: "RestoreObject",
  normalizedReason: "ConnectionFailed",
  message:
    "AD restore komutu çalıştırılamadı. Active Directory PowerShell modülü sunucuda bulunamadı.",
  targetObjectGuid: "550e8400-e29b-41d4-a716-446655440000",
  restoreOperationMode: "PowerShellRestoreAdObject",
  command: "Restore-ADObject",
  restoreTargetMode: "OriginalLocation",
  server: "dc1.example.com",
  credentialMode: "ServiceAccount",
  sanitizedPowerShellError: "ActiveDirectoryModuleNotFound",
  powerShellExitCode: 1,
  elapsedMs: 42,
};

const ldapDiagnostic = {
  code: "AD_OPERATION_FAILED",
  operation: "UserUpdate",
  step: "ApplyChanges",
  normalizedReason: "NoSuchObject",
  message: "AD kullanıcısı bulunamadı.",
  ldapResultCode: 32,
  ldapExceptionErrorCode: 32,
  ldapDiagnosticMessage: "0000208D: NameErr: DSID-0310028D",
};

describe("parseAdOperationErrorMessage", () => {
  it("parses DeletedObjectRestore PowerShell diagnostic fields", () => {
    const parsed = parseAdOperationErrorMessage(JSON.stringify(powerShellRestoreDiagnostic));

    assert.equal(parsed?.kind, "structured");
    if (parsed?.kind !== "structured") {
      return;
    }

    assert.equal(parsed.diagnostic.command, "Restore-ADObject");
    assert.equal(parsed.diagnostic.restoreOperationMode, "PowerShellRestoreAdObject");
    assert.equal(parsed.diagnostic.restoreTargetMode, "OriginalLocation");
    assert.equal(parsed.diagnostic.server, "dc1.example.com");
    assert.equal(parsed.diagnostic.credentialMode, "ServiceAccount");
    assert.equal(parsed.diagnostic.sanitizedPowerShellError, "ActiveDirectoryModuleNotFound");
    assert.equal(parsed.diagnostic.powerShellExitCode, 1);
    assert.equal(parsed.diagnostic.elapsedMs, 42);
    assert.equal(parsed.diagnostic.ldapResultCode, undefined);
    assert.equal(parsed.diagnostic.ldapExceptionErrorCode, undefined);
  });

  it("preserves LDAP diagnostic fields for non-restore operations", () => {
    const parsed = parseAdOperationErrorMessage(JSON.stringify(ldapDiagnostic));

    assert.equal(parsed?.kind, "structured");
    if (parsed?.kind !== "structured") {
      return;
    }

    assert.equal(parsed.diagnostic.ldapResultCode, 32);
    assert.equal(parsed.diagnostic.ldapExceptionErrorCode, 32);
    assert.equal(parsed.diagnostic.ldapDiagnosticMessage, "0000208D: NameErr: DSID-0310028D");
    assert.equal(parsed.diagnostic.command, undefined);
    assert.equal(parsed.diagnostic.sanitizedPowerShellError, undefined);
  });

  it("returns message summary for PowerShell module missing diagnostic", () => {
    const parsed = parseAdOperationErrorMessage(JSON.stringify(powerShellRestoreDiagnostic));
    const summary = getAdOperationErrorSummary(parsed);

    assert.equal(
      summary,
      "AD restore komutu çalıştırılamadı. Active Directory PowerShell modülü sunucuda bulunamadı.",
    );
  });
});

describe("AdOperationLogDetailDialog PowerShell diagnostic i18n", () => {
  it("includes TR and EN diagnostic labels for restore PowerShell fields", () => {
    const tr = JSON.parse(
      readFileSync(new URL("../../locales/tr/adOperationLogs.json", import.meta.url), "utf8"),
    ) as {
      adOperationLogs: { diagnostic: Record<string, string> };
    };
    const en = JSON.parse(
      readFileSync(new URL("../../locales/en/adOperationLogs.json", import.meta.url), "utf8"),
    ) as {
      adOperationLogs: { diagnostic: Record<string, string> };
    };

    const keys = [
      "command",
      "restoreOperationMode",
      "restoreTargetMode",
      "server",
      "targetPathDistinguishedName",
      "credentialMode",
      "sanitizedPowerShellError",
      "powerShellExitCode",
      "elapsedMs",
    ] as const;

    for (const key of keys) {
      assert.ok(tr.adOperationLogs.diagnostic[key], `missing TR diagnostic.${key}`);
      assert.ok(en.adOperationLogs.diagnostic[key], `missing EN diagnostic.${key}`);
    }

    assert.equal(tr.adOperationLogs.diagnostic.command, "Komut");
    assert.equal(en.adOperationLogs.diagnostic.sanitizedPowerShellError, "PowerShell error");
  });
});

describe("AdOperationLogDetailDialog diagnostic row filtering", () => {
  it("filters empty diagnostic values in detail dialog source", () => {
    const source = readFileSync(
      new URL("./components/AdOperationLogDetailDialog.tsx", import.meta.url),
      "utf8",
    );

    assert.match(source, /\.filter\(\(row\) => row\.value !== undefined && row\.value !== ""\)/);
    assert.match(source, /diagnostic\.sanitizedPowerShellError/);
    assert.match(source, /diagnostic\.command/);
    assert.match(source, /diagnostic\.restoreOperationMode/);
  });
});
