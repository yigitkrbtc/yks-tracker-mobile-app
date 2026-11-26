-- Database roles (ADR-0022 §3). Run once as the postgres superuser; idempotent (re-running also resets passwords).
-- Passwords come from the caller's environment, never the command line (docs/isletim.md §2):
--   NIRENGI_OWNER_PASSWORD=$(cat secrets/owner_password) NIRENGI_APP_PASSWORD=$(cat secrets/app_password) \
--     docker compose -f compose.prod.yml exec -T -e NIRENGI_OWNER_PASSWORD -e NIRENGI_APP_PASSWORD postgres \
--     psql -U postgres -d nirengi -f /sql/00-roles.sql
-- nirengi_owner owns schemas and tables (migrate only); nirengi_app reads and writes rows (the API, seed included).
\set ON_ERROR_STOP on
\getenv owner_password NIRENGI_OWNER_PASSWORD
\getenv app_password NIRENGI_APP_PASSWORD
\if :{?owner_password}
\else
  DO $$ BEGIN RAISE EXCEPTION 'NIRENGI_OWNER_PASSWORD is not set'; END $$;
\endif
\if :{?app_password}
\else
  DO $$ BEGIN RAISE EXCEPTION 'NIRENGI_APP_PASSWORD is not set'; END $$;
\endif

SELECT format('CREATE ROLE nirengi_owner LOGIN PASSWORD %L', :'owner_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'nirengi_owner') \gexec
SELECT format('CREATE ROLE nirengi_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'nirengi_app') \gexec
ALTER ROLE nirengi_owner PASSWORD :'owner_password';
ALTER ROLE nirengi_app PASSWORD :'app_password';

REVOKE ALL ON DATABASE nirengi FROM PUBLIC;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT, CREATE, TEMPORARY ON DATABASE nirengi TO nirengi_owner;
GRANT CONNECT ON DATABASE nirengi TO nirengi_app;

-- Every schema and table the owner creates from now on is usable by the API, DML only.
ALTER DEFAULT PRIVILEGES FOR ROLE nirengi_owner GRANT USAGE ON SCHEMAS TO nirengi_app;
ALTER DEFAULT PRIVILEGES FOR ROLE nirengi_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO nirengi_app;
ALTER DEFAULT PRIVILEGES FOR ROLE nirengi_owner GRANT USAGE, SELECT ON SEQUENCES TO nirengi_app;
