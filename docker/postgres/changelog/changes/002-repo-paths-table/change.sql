-- Create repo_paths table
-- Tracks all repository paths that have been activated
CREATE TABLE tenant_management.repo_paths (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_name VARCHAR(255) NOT NULL,
    absolute_path TEXT NOT NULL,
    path_hash CHAR(64) NOT NULL,  -- SHA-256 of absolute_path
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_repo_paths_path_hash UNIQUE (path_hash)
);

-- Add comment
COMMENT ON TABLE tenant_management.repo_paths IS 'Tracks repository paths for multi-tenant isolation';
COMMENT ON COLUMN tenant_management.repo_paths.path_hash IS 'SHA-256 hash of absolute_path for consistent tenant key';
