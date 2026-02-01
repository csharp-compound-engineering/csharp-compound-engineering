-- Create tenant_management schema
CREATE SCHEMA IF NOT EXISTS tenant_management;

-- Grant usage to application user
GRANT USAGE ON SCHEMA tenant_management TO compounding;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA tenant_management TO compounding;
ALTER DEFAULT PRIVILEGES IN SCHEMA tenant_management GRANT ALL ON TABLES TO compounding;
