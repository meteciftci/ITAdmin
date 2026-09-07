-- =============================================================================
-- Legacy License Management import - STEP 2: transform staging -> ITAdmin tables
-- =============================================================================
-- Prerequisites:
--   * 01_staging_tables.sql has run and all 8 CSVs are loaded into legacy_import.*
--   * The ITAdmin schema is at migration 20260907122312_AddLicenseSeatAssignments
--     or later (the license_seat_assignments table must exist).
--
-- Everything runs in ONE transaction. If anything fails the whole import is
-- rolled back. Every inserted row carries created_by = 'legacy-import' so it can
-- be found again (see 99_rollback.sql).
--
-- Enum columns are stored by EF as the enum MEMBER NAME (e.g. 'Active',
-- 'DirectPurchase', 'CorporateRequestSystem'); the CASE expressions below emit
-- exactly those spellings.
--
-- Mapping decisions (see README.md for the rationale):
--   applications          -> licensed_products         (single "İçe Aktarılan" category)
--   companies             -> license_companies
--   purchases             -> license_purchases         (type 1->Tender, 3->DirectPurchase, else Other; status Active)
--   purchase_applications -> license_packages          (license_type 1->Perpetual, 2->Subscription; expired end_date -> status Expired)
--   license_assignments   -> license_seat_assignments  (seat roster; status Active; predecessor names kept in note only)
--   license_requests      -> license_requests          (source 1->OfficialLetter, 2->CorporateRequestSystem; status 1->Pending,2->Draft,3->Fulfilled,4->InReview)
--   license_request_items -> license_request_items      (AGGREGATED by (request, product); quantities summed - ITAdmin has a UNIQUE (request_id, product_id))
--   license_request_users -> license_request_item_users (attached to the aggregated item; ad_object_id = 'legacy:<tcno>')
-- =============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Guard: refuse to run twice.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM licensed_products WHERE created_by = 'legacy-import')
       OR EXISTS (SELECT 1 FROM license_companies WHERE created_by = 'legacy-import') THEN
        RAISE EXCEPTION 'legacy-import rows already exist. Run 99_rollback.sql first if you want to re-import.';
    END IF;
END $$;

-- -----------------------------------------------------------------------------
-- 0. id maps (legacy bigint -> new uuid)
-- -----------------------------------------------------------------------------
CREATE TEMP TABLE _cat ON COMMIT DROP AS SELECT gen_random_uuid() AS id;

CREATE TEMP TABLE _map_product ON COMMIT DROP AS
SELECT a.id AS old_id, gen_random_uuid() AS new_id FROM legacy_import.applications a;

CREATE TEMP TABLE _map_company ON COMMIT DROP AS
SELECT c.id AS old_id, gen_random_uuid() AS new_id FROM legacy_import.companies c;

CREATE TEMP TABLE _map_purchase ON COMMIT DROP AS
SELECT p.id AS old_id, gen_random_uuid() AS new_id FROM legacy_import.purchases p;

CREATE TEMP TABLE _map_package ON COMMIT DROP AS
SELECT pa.id AS old_id, gen_random_uuid() AS new_id FROM legacy_import.purchase_applications pa;

CREATE TEMP TABLE _map_assignment ON COMMIT DROP AS
SELECT la.id AS old_id, gen_random_uuid() AS new_id FROM legacy_import.license_assignments la;

CREATE TEMP TABLE _map_request ON COMMIT DROP AS
SELECT r.id AS old_id, gen_random_uuid() AS new_id FROM legacy_import.license_requests r;

-- Aggregated request items: one row per (request, product).
CREATE TEMP TABLE _grp_req_item ON COMMIT DROP AS
SELECT ri.license_request_id            AS old_req,
       ri.application_id                AS old_app,
       SUM(COALESCE(ri.quantity, 1))::int AS qty,
       MIN(ri.created_at)              AS created_at,
       gen_random_uuid()              AS new_id
FROM legacy_import.license_request_items ri
GROUP BY ri.license_request_id, ri.application_id;

-- legacy request_item.id -> aggregated item uuid (for attaching users)
CREATE TEMP TABLE _map_req_item ON COMMIT DROP AS
SELECT ri.id AS old_item_id, g.new_id AS new_id
FROM legacy_import.license_request_items ri
JOIN _grp_req_item g
  ON g.old_req = ri.license_request_id AND g.old_app = ri.application_id;

-- -----------------------------------------------------------------------------
-- 1. category (parent required by licensed_products.category_id)
-- -----------------------------------------------------------------------------
INSERT INTO license_product_categories (id, name, description, is_active, created_at, created_by)
SELECT id, 'İçe Aktarılan', 'Eski lisans uygulamasından aktarılan ürünler', TRUE, now(), 'legacy-import'
FROM _cat;

-- -----------------------------------------------------------------------------
-- 2. products  (applications)
-- -----------------------------------------------------------------------------
INSERT INTO licensed_products
    (id, name, brand, category_id, description, is_active, created_at, created_by, updated_at, updated_by)
SELECT m.new_id,
       LEFT(COALESCE(NULLIF(TRIM(a.name), ''), '(isimsiz ürün)'), 200),
       LEFT(NULLIF(TRIM(a.brand), ''), 200),
       (SELECT id FROM _cat),
       'legacy application id: ' || a.id,
       TRUE,
       COALESCE(a.created_at, now()),
       'legacy-import',
       a.updated_at,
       CASE WHEN a.updated_at IS NOT NULL THEN 'legacy-import' END
FROM legacy_import.applications a
JOIN _map_product m ON m.old_id = a.id;

-- -----------------------------------------------------------------------------
-- 3. companies
-- -----------------------------------------------------------------------------
INSERT INTO license_companies
    (id, name, phone, email, website, contact_person_name, contact_person_phone,
     contact_person_email, notes, is_active, created_at, created_by, updated_at, updated_by)
SELECT m.new_id,
       LEFT(COALESCE(NULLIF(TRIM(c.name), ''), '(isimsiz firma)'), 200),
       LEFT(NULLIF(TRIM(c.phone), ''), 50),
       LEFT(NULLIF(TRIM(c.email), ''), 250),
       NULL,
       LEFT(NULLIF(TRIM(c.contact_person), ''), 200),
       NULL,
       NULL,
       'legacy company id: ' || c.id,
       TRUE,
       COALESCE(c.created_at, now()),
       'legacy-import',
       c.updated_at,
       CASE WHEN c.updated_at IS NOT NULL THEN 'legacy-import' END
FROM legacy_import.companies c
JOIN _map_company m ON m.old_id = c.id;

-- -----------------------------------------------------------------------------
-- 4. purchases
-- -----------------------------------------------------------------------------
INSERT INTO license_purchases
    (id, purchase_type, title, description, purchase_date, tender_number,
     direct_purchase_number, status, notes, created_at, created_by, updated_at, updated_by)
SELECT m.new_id,
       CASE p.procurement_type WHEN 1 THEN 'Tender' WHEN 3 THEN 'DirectPurchase' ELSE 'Other' END,
       LEFT(COALESCE(NULLIF(TRIM(p.name), ''), 'Satın alma ' || p.id), 300),
       NULL,
       p.purchase_date::date,
       CASE WHEN p.procurement_type = 1 THEN LEFT(NULLIF(TRIM(p.procurement_number), ''), 100) END,
       CASE WHEN p.procurement_type = 3 THEN LEFT(NULLIF(TRIM(p.procurement_number), ''), 100) END,
       'Active',
       LEFT('legacy purchase id: ' || p.id
            || CASE WHEN p.procurement_type NOT IN (1, 3) AND NULLIF(TRIM(p.procurement_number), '') IS NOT NULL
                    THEN ' / belge no: ' || TRIM(p.procurement_number) ELSE '' END, 4000),
       COALESCE(p.created_at, now()),
       'legacy-import',
       p.updated_at,
       CASE WHEN p.updated_at IS NOT NULL THEN 'legacy-import' END
FROM legacy_import.purchases p
JOIN _map_purchase m ON m.old_id = p.id;

-- -----------------------------------------------------------------------------
-- 5. packages  (purchase_applications)
-- -----------------------------------------------------------------------------
INSERT INTO license_packages
    (id, purchase_id, product_id, license_type, quantity, start_date, end_date,
     is_perpetual, renewal_required, renewal_date, serial_number, license_key,
     license_account_email, license_portal_url, license_notes, is_active, status,
     created_at, created_by, updated_at, updated_by)
SELECT m.new_id,
       mp.new_id,
       mpr.new_id,
       CASE pa.license_type WHEN 1 THEN 'Perpetual' WHEN 2 THEN 'Subscription' ELSE 'Other' END,
       GREATEST(COALESCE(pa.user_count, 1), 1),
       NULL,
       pa.license_end_date::date,
       (pa.license_type = 1),
       (pa.license_end_date IS NOT NULL),
       NULL,
       NULL, NULL, NULL, NULL,
       LEFT(NULLIF(TRIM(
            COALESCE(pa.license_detail, '')
            || CASE WHEN NULLIF(TRIM(lc.name), '') IS NOT NULL THEN E'\n' || 'Tedarikçi: ' || TRIM(lc.name) ELSE '' END
            || E'\n' || 'legacy purchase_application id: ' || pa.id
       ), ''), 4000),
       TRUE,
       CASE WHEN pa.license_end_date IS NOT NULL AND pa.license_end_date::date < CURRENT_DATE
            THEN 'Expired' ELSE 'Active' END,
       COALESCE(pa.created_at, now()),
       'legacy-import',
       pa.updated_at,
       CASE WHEN pa.updated_at IS NOT NULL THEN 'legacy-import' END
FROM legacy_import.purchase_applications pa
JOIN _map_package  m   ON m.old_id   = pa.id
JOIN _map_purchase mp  ON mp.old_id  = pa.purchase_id
JOIN _map_product  mpr ON mpr.old_id = pa.application_id
LEFT JOIN legacy_import.companies lc ON lc.id = pa.company_id;

-- -----------------------------------------------------------------------------
-- 6. seat assignments  (license_assignments)
--    Legacy has no "released" state, so every imported seat is Active. Phrases
--    like "... yerine atandı" are preserved in note; the replaces_assignment_id
--    transfer chain is left NULL and gets populated from now on by the app's
--    Transfer action.
-- -----------------------------------------------------------------------------
INSERT INTO license_seat_assignments
    (id, package_id, ad_object_id, display_name, sam_account_name, user_principal_name,
     mail, national_id, department, title, assigned_date, released_date, status,
     replaces_assignment_id, source_request_item_id, note, created_at, created_by, updated_at, updated_by)
SELECT ma.new_id,
       mp.new_id,
       NULL,
       LEFT(COALESCE(NULLIF(TRIM(la.assigned_user_name), ''), '(bilinmiyor)'), 200),
       LEFT(NULLIF(split_part(COALESCE(la.assigned_email, ''), '@', 1), ''), 100),
       LEFT(NULLIF(TRIM(la.assigned_email), ''), 250),
       LEFT(NULLIF(TRIM(la.assigned_email), ''), 250),
       LEFT(NULLIF(TRIM(la.assigned_user_tcno), ''), 20),
       NULL, NULL,
       COALESCE(la.assigned_date::date, la.created_at::date, CURRENT_DATE),
       NULL,
       'Active',
       NULL,
       NULL,
       LEFT(NULLIF(TRIM(COALESCE(la.description, '') || E'\n' || 'legacy assignment id: ' || la.id), ''), 4000),
       COALESCE(la.created_at, now()),
       'legacy-import',
       la.updated_at,
       CASE WHEN la.updated_at IS NOT NULL THEN 'legacy-import' END
FROM legacy_import.license_assignments la
JOIN _map_assignment ma ON ma.old_id = la.id
JOIN _map_package    mp ON mp.old_id = la.purchase_application_id;   -- orphan assignments (no package) are skipped; see 03_verify.sql

-- -----------------------------------------------------------------------------
-- 7. requests
-- -----------------------------------------------------------------------------
INSERT INTO license_requests
    (id, request_source, request_date, external_request_number, ebys_number, ebys_date,
     requester_unit_display_name, requester_unit_distinguished_name, requester_unit_object_guid,
     requester_manager_name, description, status, is_active,
     created_at, created_by, updated_at, updated_by)
SELECT m.new_id,
       CASE r.source WHEN 1 THEN 'OfficialLetter' WHEN 2 THEN 'CorporateRequestSystem' ELSE 'Other' END,
       COALESCE(r.request_date::date, r.created_at::date, CURRENT_DATE),
       LEFT(NULLIF(TRIM(r.request_number), ''), 100),
       NULL, NULL,
       'Bilinmiyor (içe aktarıldı)',
       '',
       '',
       NULL,
       LEFT(NULLIF(TRIM(r.description), ''), 4000),
       CASE r.status WHEN 1 THEN 'Pending' WHEN 2 THEN 'Draft' WHEN 3 THEN 'Fulfilled' WHEN 4 THEN 'InReview' ELSE 'Pending' END,
       TRUE,
       COALESCE(r.created_at, now()),
       'legacy-import',
       r.updated_at,
       CASE WHEN r.updated_at IS NOT NULL THEN 'legacy-import' END
FROM legacy_import.license_requests r
JOIN _map_request m ON m.old_id = r.id;

-- -----------------------------------------------------------------------------
-- 8. request items  (aggregated)
-- -----------------------------------------------------------------------------
INSERT INTO license_request_items
    (id, request_id, product_id, requested_quantity, approved_quantity, fulfilled_quantity,
     status, created_at, created_by)
SELECT g.new_id,
       mr.new_id,
       mpr.new_id,
       g.qty,
       NULL,
       0,
       CASE lr.status WHEN 3 THEN 'Fulfilled' ELSE 'Pending' END,
       COALESCE(g.created_at, now()),
       'legacy-import'
FROM _grp_req_item g
JOIN _map_request  mr  ON mr.old_id  = g.old_req
JOIN _map_product  mpr ON mpr.old_id = g.old_app
JOIN legacy_import.license_requests lr ON lr.id = g.old_req;

-- -----------------------------------------------------------------------------
-- 9. request item users
-- -----------------------------------------------------------------------------
INSERT INTO license_request_item_users
    (id, request_item_id, ad_object_id, sam_account_name, user_principal_name, display_name,
     department, title, mail, phone, status, created_at, created_by)
SELECT DISTINCT ON (mi.new_id, 'legacy:' || COALESCE(NULLIF(TRIM(u.user_tcno), ''), 'row' || u.id))
       gen_random_uuid(),
       mi.new_id,
       LEFT('legacy:' || COALESCE(NULLIF(TRIM(u.user_tcno), ''), 'row' || u.id), 100),
       LEFT(NULLIF(split_part(COALESCE(u.email, ''), '@', 1), ''), 100),
       LEFT(NULLIF(TRIM(u.email), ''), 250),
       LEFT(COALESCE(NULLIF(TRIM(u.user_name), ''), '(bilinmiyor)'), 200),
       NULL, NULL,
       LEFT(NULLIF(TRIM(u.email), ''), 250),
       NULL,
       CASE lr.status WHEN 3 THEN 'Fulfilled' ELSE 'Pending' END,
       COALESCE(u.created_at, now()),
       'legacy-import'
FROM legacy_import.license_request_users u
JOIN _map_req_item mi ON mi.old_item_id = u.license_request_item_id
JOIN _grp_req_item g  ON g.new_id = mi.new_id
JOIN legacy_import.license_requests lr ON lr.id = g.old_req
ORDER BY mi.new_id,
         'legacy:' || COALESCE(NULLIF(TRIM(u.user_tcno), ''), 'row' || u.id),
         u.id;

-- -----------------------------------------------------------------------------
-- Row-count summary before commit
-- -----------------------------------------------------------------------------
SELECT 'license_product_categories' AS table, count(*) FROM license_product_categories WHERE created_by = 'legacy-import'
UNION ALL SELECT 'licensed_products',           count(*) FROM licensed_products           WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_companies',           count(*) FROM license_companies           WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_purchases',           count(*) FROM license_purchases           WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_packages',            count(*) FROM license_packages            WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_seat_assignments',    count(*) FROM license_seat_assignments    WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_requests',            count(*) FROM license_requests            WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_request_items',       count(*) FROM license_request_items       WHERE created_by = 'legacy-import'
UNION ALL SELECT 'license_request_item_users',  count(*) FROM license_request_item_users  WHERE created_by = 'legacy-import';

-- Review the counts above. If they look right:
--     COMMIT;
-- otherwise:
--     ROLLBACK;
-- (This script intentionally does NOT auto-commit.)
