-- =============================================================================
-- Legacy License Management import - ROLLBACK
-- =============================================================================
-- Removes every row the import created (created_by = 'legacy-import'), in FK
-- dependency order. Use this to redo the import, or to back the data out.
--
-- It does NOT touch the legacy_import staging schema - drop that separately with
--   DROP SCHEMA legacy_import CASCADE;
-- once you no longer need it.
--
-- WARNING: if you have since edited any imported row through the app, its
-- created_by is still 'legacy-import' (the app only changes updated_by), so it
-- will also be deleted. Check first:
--   SELECT count(*) FROM license_seat_assignments WHERE created_by='legacy-import' AND updated_by IS NOT NULL;
-- =============================================================================

BEGIN;

DELETE FROM license_request_item_users     WHERE created_by = 'legacy-import';
DELETE FROM license_request_items          WHERE created_by = 'legacy-import';
DELETE FROM license_requests               WHERE created_by = 'legacy-import';

-- seat assignments can reference each other via replaces_assignment_id; two passes.
UPDATE license_seat_assignments SET replaces_assignment_id = NULL WHERE created_by = 'legacy-import';
DELETE FROM license_seat_assignments       WHERE created_by = 'legacy-import';

DELETE FROM license_packages               WHERE created_by = 'legacy-import';
DELETE FROM license_purchases              WHERE created_by = 'legacy-import';
DELETE FROM licensed_products              WHERE created_by = 'legacy-import';
DELETE FROM license_product_categories     WHERE created_by = 'legacy-import';
DELETE FROM license_companies              WHERE created_by = 'legacy-import';

-- Review, then COMMIT; or ROLLBACK;
