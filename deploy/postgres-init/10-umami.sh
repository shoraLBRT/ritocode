#!/bin/sh
# Umami's role and database, beside the product's on the same server (docs/SPEC.md §9.5). Run by the
# postgres image once, when its volume is first created; on an existing volume it does nothing, and
# the role and database are made by hand (deploy/README.md).
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
	--set umami_password="$UMAMI_DB_PASSWORD" <<'SQL'
CREATE ROLE umami LOGIN PASSWORD :'umami_password';
CREATE DATABASE umami OWNER umami;
SQL
