import assert from "node:assert/strict";
import { test } from "node:test";

import { collectAllPagedItems } from "./paged-items.ts";

test("collectAllPagedItems returns every page in stable page order", async () => {
  const requestedPages: number[] = [];
  const items = await collectAllPagedItems(2, async (pageNumber, pageSize) => {
    requestedPages.push(pageNumber);
    assert.equal(pageSize, 2);
    return {
      items: pageNumber === 1 ? ["a", "b"] : pageNumber === 2 ? ["c", "d"] : ["e"],
      totalPages: 3,
    };
  });

  assert.deepEqual(requestedPages, [1, 2, 3]);
  assert.deepEqual(items, ["a", "b", "c", "d", "e"]);
});

test("collectAllPagedItems stops after the first page when no more pages exist", async () => {
  let callCount = 0;
  const items = await collectAllPagedItems(100, async () => {
    callCount += 1;
    return { items: [1], totalPages: 1 };
  });

  assert.equal(callCount, 1);
  assert.deepEqual(items, [1]);
});
