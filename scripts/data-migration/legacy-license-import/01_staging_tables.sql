-- =============================================================================
-- Legacy License Management import - STEP 1: staging tables
-- =============================================================================
-- Run this once in pgAdmin4 Query Tool against the ITAdmin database.
-- It creates a separate schema "legacy_import" holding one table per CSV, with
-- columns in the SAME ORDER as the CSV headers so pgAdmin's Import/Export tool
-- can load each file without column mapping.
--
-- After running this file:
--   1. Right-click legacy_import.companies -> Import/Export Data...
--      - Import, Format: csv, Header: yes, Delimiter: ","  Quote: "  Encoding: UTF8
--      - Columns tab: leave all (order already matches)
--   2. Repeat for every table below with its matching CSV.
--   3. Then run 02_transform.sql
--
-- CSV  ->  TABLE
--   companies.csv              -> legacy_import.companies
--   applications.csv           -> legacy_import.applications
--   purchases.csv              -> legacy_import.purchases
--   purchase_applications.csv  -> legacy_import.purchase_applications
--   license_assignments.csv    -> legacy_import.license_assignments
--   license_requests.csv       -> legacy_import.license_requests
--   license_request_items.csv  -> legacy_import.license_request_items
--   license_request_users.csv  -> legacy_import.license_request_users
-- =============================================================================

DROP SCHEMA IF EXISTS legacy_import CASCADE;
CREATE SCHEMA legacy_import;

-- companies.csv: id,name,email,phone,created_at,updated_at,created_by,updated_by,contact_person
CREATE TABLE legacy_import.companies (
    id             bigint,
    name           text,
    email          text,
    phone          text,
    created_at     timestamptz,
    updated_at     timestamptz,
    created_by     text,
    updated_by     text,
    contact_person text
);

-- applications.csv: id,name,brand,created_at,updated_at,created_by,updated_by
CREATE TABLE legacy_import.applications (
    id         bigint,
    name       text,
    brand      text,
    created_at timestamptz,
    updated_at timestamptz,
    created_by text,
    updated_by text
);

-- purchases.csv: id,procurement_type,procurement_number,purchase_date,created_at,updated_at,created_by,updated_by,name
CREATE TABLE legacy_import.purchases (
    id                bigint,
    procurement_type  integer,
    procurement_number text,
    purchase_date     timestamptz,
    created_at        timestamptz,
    updated_at        timestamptz,
    created_by        text,
    updated_by        text,
    name              text
);

-- purchase_applications.csv:
--   id,purchase_id,application_id,user_count,license_type,license_end_date,
--   created_at,updated_at,created_by,updated_by,company_id,license_detail
CREATE TABLE legacy_import.purchase_applications (
    id               bigint,
    purchase_id      bigint,
    application_id   bigint,
    user_count       integer,
    license_type     integer,
    license_end_date timestamptz,
    created_at       timestamptz,
    updated_at       timestamptz,
    created_by       text,
    updated_by       text,
    company_id       bigint,
    license_detail   text
);

-- license_assignments.csv:
--   id,purchase_application_id,assigned_date,assigned_email,description,
--   created_at,updated_at,created_by,updated_by,assigned_user_name,assigned_user_tcno
CREATE TABLE legacy_import.license_assignments (
    id                     bigint,
    purchase_application_id bigint,
    assigned_date          timestamptz,
    assigned_email         text,
    description            text,
    created_at             timestamptz,
    updated_at             timestamptz,
    created_by             text,
    updated_by             text,
    assigned_user_name     text,
    assigned_user_tcno     text
);

-- license_requests.csv: id,source,request_number,request_date,description,status,created_at,updated_at,created_by,updated_by
CREATE TABLE legacy_import.license_requests (
    id             bigint,
    source         integer,
    request_number text,
    request_date   timestamptz,
    description    text,
    status         integer,
    created_at     timestamptz,
    updated_at     timestamptz,
    created_by     text,
    updated_by     text
);

-- license_request_items.csv: id,license_request_id,application_id,quantity,created_at,updated_at,created_by,updated_by
CREATE TABLE legacy_import.license_request_items (
    id                bigint,
    license_request_id bigint,
    application_id     bigint,
    quantity          integer,
    created_at        timestamptz,
    updated_at        timestamptz,
    created_by        text,
    updated_by        text
);

-- license_request_users.csv: id,license_request_item_id,user_name,user_tcno,email,created_at,created_by
CREATE TABLE legacy_import.license_request_users (
    id                     bigint,
    license_request_item_id bigint,
    user_name              text,
    user_tcno              text,
    email                  text,
    created_at             timestamptz,
    created_by             text
);

-- Quick check after you have imported all 8 CSVs:
--   SELECT 'companies' t, count(*) FROM legacy_import.companies
--   UNION ALL SELECT 'applications', count(*) FROM legacy_import.applications
--   UNION ALL SELECT 'purchases', count(*) FROM legacy_import.purchases
--   UNION ALL SELECT 'purchase_applications', count(*) FROM legacy_import.purchase_applications
--   UNION ALL SELECT 'license_assignments', count(*) FROM legacy_import.license_assignments
--   UNION ALL SELECT 'license_requests', count(*) FROM legacy_import.license_requests
--   UNION ALL SELECT 'license_request_items', count(*) FROM legacy_import.license_request_items
--   UNION ALL SELECT 'license_request_users', count(*) FROM legacy_import.license_request_users;
