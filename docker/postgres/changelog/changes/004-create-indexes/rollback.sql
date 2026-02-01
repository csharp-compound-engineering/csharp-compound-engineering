-- Drop indexes for branches table
DROP INDEX IF EXISTS tenant_management.idx_branches_is_default;
DROP INDEX IF EXISTS tenant_management.idx_branches_last_accessed;
DROP INDEX IF EXISTS tenant_management.idx_branches_repo_path_id;

-- Drop indexes for repo_paths table
DROP INDEX IF EXISTS tenant_management.idx_repo_paths_last_accessed;
DROP INDEX IF EXISTS tenant_management.idx_repo_paths_project_name;
