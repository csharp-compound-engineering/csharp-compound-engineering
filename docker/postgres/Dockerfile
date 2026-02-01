FROM pgvector/pgvector:pg16

# Install Java runtime for Liquibase and wget for downloading
RUN apt-get update && \
    apt-get install -y --no-install-recommends wget default-jre-headless && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Install Liquibase 4.25.0
ENV LIQUIBASE_VERSION=4.25.0
RUN wget -qO- "https://github.com/liquibase/liquibase/releases/download/v${LIQUIBASE_VERSION}/liquibase-${LIQUIBASE_VERSION}.tar.gz" | \
    tar xz -C /opt && \
    ln -s /opt/liquibase /usr/local/bin/liquibase

# Download PostgreSQL JDBC driver
RUN mkdir -p /opt/liquibase/lib && \
    wget -qO /opt/liquibase/lib/postgresql.jar \
    "https://jdbc.postgresql.org/download/postgresql-42.7.1.jar"

# Copy initialization scripts
COPY init-db.sh /docker-entrypoint-initdb.d/01-init-db.sh
RUN chmod +x /docker-entrypoint-initdb.d/01-init-db.sh

# Create changelog directory
RUN mkdir -p /liquibase/changelog
