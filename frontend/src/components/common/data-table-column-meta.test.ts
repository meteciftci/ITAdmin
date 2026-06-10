import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { describe, it } from "node:test";

const dataTableSource = readFileSync(
  new URL("./data-table.tsx", import.meta.url),
  "utf8",
);

describe("data-table column meta", () => {
  it("supports align on DataTableColumnMeta", () => {
    assert.match(dataTableSource, /align\?: DataTableColumnAlign/);
    assert.match(dataTableSource, /"left" \| "center" \| "right"/);
  });

  it("applies text-center for align center on header and cell", () => {
    assert.match(dataTableSource, /function getAlignClassName/);
    assert.match(dataTableSource, /align === "center"[\s\S]*return "text-center"/);
    assert.match(dataTableSource, /getAlignClassName\(getEffectiveAlign\(meta\)\)/);
  });

  it("applies compact width classes for isAction columns", () => {
    assert.match(dataTableSource, /ACTION_COLUMN_WIDTH_CLASS/);
    assert.match(dataTableSource, /w-0 whitespace-nowrap overflow-visible/);
    assert.match(dataTableSource, /meta\?\.isAction \? ACTION_COLUMN_WIDTH_CLASS/);
    assert.match(dataTableSource, /meta\?\.isAction && ACTION_COLUMN_WIDTH_CLASS/);
  });

  it("uses justify-center content wrapper for isAction with align center", () => {
    assert.match(dataTableSource, /function getCellContentClassName/);
    assert.match(dataTableSource, /align === "center"[\s\S]*return "flex justify-center"/);
    assert.match(dataTableSource, /getCellContentClassName\(meta\)/);
  });

  it("defaults isAction content alignment to justify-end when align is omitted", () => {
    assert.match(dataTableSource, /if \(meta\?\.isAction\) \{[\s\S]*return "right"/);
    assert.match(dataTableSource, /return "flex justify-end"/);
  });
});
