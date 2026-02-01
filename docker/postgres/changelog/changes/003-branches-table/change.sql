-- Create branches table
-- Tracks git branches for each repository
CREATE TABLE tenant_management.branches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    repo_path_id UUID NOT NULL REFERENCES tenant_management.repo_paths(id) ON DELETE CASCADE,
    branch_name VARCHAR(255) NOT NULL,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_accessed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_branches_repo_branch UNIQUE (repo_path_id, branch_name)
);

-- Add comments
COMMENT ON TABLE tenant_management.branches IS 'Tracks git branches per repository for tenant isolation';
COMMENT ON COLUMN tenant_management.branches.is_default IS 'True if this is the default branch (main/master)';
