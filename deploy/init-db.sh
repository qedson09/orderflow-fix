#!/bin/sh
set -eu
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v generator_password="$GENERATOR_PASSWORD" -v accumulator_password="$ACCUMULATOR_PASSWORD" <<'SQL'
SELECT format('CREATE ROLE generator_app LOGIN PASSWORD %L', :'generator_password') \gexec
SELECT format('CREATE ROLE accumulator_app LOGIN PASSWORD %L', :'accumulator_password') \gexec
REVOKE CREATE ON DATABASE orderflow FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA generator AUTHORIZATION generator_app;
CREATE SCHEMA accumulator AUTHORIZATION accumulator_app;
SQL
