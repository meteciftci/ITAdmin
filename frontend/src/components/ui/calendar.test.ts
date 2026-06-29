import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

const root = join(dirname(fileURLToPath(import.meta.url)), "../..");

test("Calendar hides visible caption label when dropdown caption layout is used", () => {
  const calendarSource = readFileSync(join(root, "components/ui/calendar.tsx"), "utf8");

  assert.match(calendarSource, /usesDropdownCaption/);
  assert.match(calendarSource, /captionLayout\.startsWith\("dropdown"\)/);
  assert.match(calendarSource, /caption_label: dropdownCaption \? "sr-only" : "text-sm font-medium"/);
});

test("Calendar keeps visible caption label for non-dropdown caption layout", () => {
  const calendarSource = readFileSync(join(root, "components/ui/calendar.tsx"), "utf8");

  assert.match(calendarSource, /caption_label: dropdownCaption \? "sr-only" : "text-sm font-medium"/);
  assert.doesNotMatch(calendarSource, /caption_label: "sr-only"/);
});

test("Calendar places dropdown caption controls on one header row", () => {
  const calendarSource = readFileSync(join(root, "components/ui/calendar.tsx"), "utf8");

  assert.match(calendarSource, /grid-cols-\[auto_minmax\(0,1fr\)_auto\]/);
  assert.match(calendarSource, /button_previous:[\s\S]*col-start-1 row-start-1/);
  assert.match(calendarSource, /month_caption:[\s\S]*col-start-2 row-start-1/);
  assert.match(calendarSource, /button_next:[\s\S]*col-start-3 row-start-1/);
  assert.match(calendarSource, /month_grid: dropdownCaption \? "col-span-3 row-start-2"/);
});

test("Calendar uses non-absolute nav buttons for dropdown caption layout", () => {
  const calendarSource = readFileSync(join(root, "components/ui/calendar.tsx"), "utf8");

  assert.match(calendarSource, /dropdownCaption[\s\S]*shrink-0/);
  assert.match(calendarSource, /dropdownCaption[\s\S]*absolute left-1/);
  assert.match(calendarSource, /dropdownCaption[\s\S]*absolute right-1/);
});

test("DatePicker uses dropdown caption with navLayout after and no duplicate caption text", () => {
  const datePickerSource = readFileSync(join(root, "components/common/DatePicker.tsx"), "utf8");
  const calendarSource = readFileSync(join(root, "components/ui/calendar.tsx"), "utf8");

  assert.match(datePickerSource, /captionLayout="dropdown"/);
  assert.match(datePickerSource, /navLayout="around"/);
  assert.match(datePickerSource, /datePicker\.today/);
  assert.match(datePickerSource, /onChange\(null\)/);
  assert.match(datePickerSource, /PopoverTrigger asChild fullWidth/);
  assert.match(calendarSource, /caption_label: dropdownCaption \? "sr-only"/);
});

test("DateRangePicker keeps default caption label behavior without dropdown layout", () => {
  const dateRangePickerSource = readFileSync(
    join(root, "components/common/DateRangePicker.tsx"),
    "utf8",
  );

  assert.doesNotMatch(dateRangePickerSource, /captionLayout/);
});
