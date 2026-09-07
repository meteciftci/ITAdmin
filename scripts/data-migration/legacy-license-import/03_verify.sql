-- =============================================================================
-- Legacy License Management import - STEP 3: verification
-- =============================================================================
-- Safe to run before OR after COMMIT. Read-only. Compares legacy staging counts
-- with what landed in the ITAdmin tables and lists anything that was dropped.
-- =============================================================================

-- 1. Row counts: legacy vs imported ------------------------------------------------
SELECT 'products'          AS entity,
       (SELECT count(*) FROM legacy_import.applications)            AS legacy,
       (SELECT count(*) FROM licensed_products WHERE created_by='legacy-import') AS imported
UNION ALL
SELECT 'companies',
       (SELECT count(*) FROM legacy_import.companies),
       (SELECT count(*) FROM license_companies WHERE created_by='legacy-import')
UNION ALL
SELECT 'purchases',
       (SELECT count(*) FROM legacy_import.purchases),
       (SELECT count(*) FROM license_purchases WHERE created_by='legacy-import')
UNION ALL
SELECT 'packages',
       (SELECT count(*) FROM legacy_import.purchase_applications),
       (SELECT count(*) FROM license_packages WHERE created_by='legacy-import')
UNION ALL
SELECT 'seat_assignments',
       (SELECT count(*) FROM legacy_import.license_assignments),
       (SELECT count(*) FROM license_seat_assignments WHERE created_by='legacy-import')
UNION ALL
SELECT 'requests',
       (SELECT count(*) FROM legacy_import.license_requests),
       (SELECT count(*) FROM license_requests WHERE created_by='legacy-import')
UNION ALL
SELECT 'request_items (legacy rows -> aggregated)',
       (SELECT count(*) FROM legacy_import.license_request_items),
       (SELECT count(*) FROM license_request_items WHERE created_by='legacy-import')
UNION ALL
SELECT 'request_item_users',
       (SELECT count(*) FROM legacy_import.license_request_users),
       (SELECT count(*) FROM license_request_item_users WHERE created_by='legacy-import');

-- 2. Dropped rows: seat assignments whose package_id has no matching purchase_application
SELECT la.id AS orphan_assignment_id, la.purchase_application_id, la.assigned_user_name, la.assigned_email
FROM legacy_import.license_assignments la
LEFT JOIN legacy_import.purchase_applications pa ON pa.id = la.purchase_application_id
WHERE pa.id IS NULL;

-- 3. Dropped rows: request items pointing at a missing request or application
SELECT ri.id AS orphan_request_item_id, ri.license_request_id, ri.application_id
FROM legacy_import.license_request_items ri
LEFT JOIN legacy_import.license_requests   r ON r.id = ri.license_request_id
LEFT JOIN legacy_import.applications       a ON a.id = ri.application_id
WHERE r.id IS NULL OR a.id IS NULL;

-- 4. Dropped rows: request users pointing at a missing request item
SELECT u.id AS orphan_user_id, u.license_request_item_id, u.user_name
FROM legacy_import.license_request_users u
LEFT JOIN legacy_import.license_request_items ri ON ri.id = u.license_request_item_id
WHERE ri.id IS NULL;

-- 5. Packages: seat usage vs capacity (active seats over quantity = over-allocated in legacy)
SELECT p.id, pr.name AS product, p.quantity,
       count(sa.*) FILTER (WHERE sa.status = 'Active') AS active_seats,
       p.quantity - count(sa.*) FILTER (WHERE sa.status = 'Active') AS free_seats,
       p.status
FROM license_packages p
JOIN licensed_products pr ON pr.id = p.product_id
LEFT JOIN license_seat_assignments sa ON sa.package_id = p.id
WHERE p.created_by = 'legacy-import'
GROUP BY p.id, pr.name, p.quantity, p.status
ORDER BY free_seats;

-- 6. Spot check: a request with its aggregated items and user counts
SELECT r.external_request_number, r.status,
       pr.name AS product, ri.requested_quantity,
       count(u.*) AS users
FROM license_requests r
JOIN license_request_items ri ON ri.request_id = r.id
JOIN licensed_products pr ON pr.id = ri.product_id
LEFT JOIN license_request_item_users u ON u.request_item_id = ri.id
WHERE r.created_by = 'legacy-import'
GROUP BY r.external_request_number, r.status, pr.name, ri.requested_quantity
ORDER BY r.external_request_number, pr.name;
