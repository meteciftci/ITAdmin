import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { buildAdOperationLogsPath } from "./operation-logs-path.ts";

describe("buildAdOperationLogsPath", () => {
  it("appends targetObjectGuid query when provided", () => {
    const guid = "550e8400-e29b-41d4-a716-446655440000";
    assert.equal(
      buildAdOperationLogsPath(guid),
      `/monitoring/module-logs/ad-operation-logs?targetObjectGuid=${guid}`,
    );
  });

  it("returns base path without query when guid is empty", () => {
    assert.equal(
      buildAdOperationLogsPath(""),
      "/monitoring/module-logs/ad-operation-logs",
    );
  });
});
