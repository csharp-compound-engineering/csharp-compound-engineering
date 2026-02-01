-- Indexes for repo_paths table
CREATE INDEX idx_repo_paths_project_name ON tenant_management.repo_paths(project_name);
CREATE INDEX idx_repo_paths_last_accessed ON tenant_management.repo_paths(last_accessed_at);

-- Indexes for branches table
CREATE INDEX idx_branches_repo_path_id ON tenant_management.branches(repo_path_id);
CREATE INDEX idx_branches_last_accessed ON tenant_management.branches(last_accessed_at);
CREATE INDEX idx_branches_is_default ON tenant_management.branches(is_default) WHERE is_default = TRUE;
