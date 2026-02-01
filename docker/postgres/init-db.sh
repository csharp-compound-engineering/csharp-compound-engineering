#!/bin/bash
set -e

echo "Enabling pgvector extension..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE EXTENSION IF NOT EXISTS vector;
EOSQL

echo "Running Liquibase migrations..."
if [ -f "/liquibase/changelog/changelog.xml" ]; then
    /opt/liquibase/liquibase \
        --changeLogFile=/liquibase/changelog/changelog.xml \
        --url="jdbc:postgresql://localhost:5432/$POSTGRES_DB" \
        --username="$POSTGRES_USER" \
        --password="$POSTGRES_PASSWORD" \
        --classpath="/opt/liquibase/lib/postgresql.jar" \
        update
    echo "Liquibase migrations completed."
else
    echo "No changelog.xml found, skipping Liquibase migrations."
fi

echo "Database initialization complete."
