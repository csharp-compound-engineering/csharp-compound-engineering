################################################################################
# Outputs — Phase 2: Data
################################################################################

# ------------------------------------------------------------------------------
# Neptune
# ------------------------------------------------------------------------------

output "neptune_endpoint" {
  description = "Writer endpoint for the Neptune cluster"
  value       = module.neptune.endpoint
}

output "neptune_port" {
  description = "Port number for the Neptune cluster"
  value       = module.neptune.port
}

# ------------------------------------------------------------------------------
# OpenSearch
# ------------------------------------------------------------------------------

output "opensearch_endpoint" {
  description = "HTTPS endpoint for the OpenSearch domain"
  value       = module.opensearch.domain_endpoint
}

# ------------------------------------------------------------------------------
# EFS
# ------------------------------------------------------------------------------

output "efs_filesystem_id" {
  description = "ID of the EFS file system for Git repositories"
  value       = aws_efs_file_system.git_repos.id
}

output "efs_access_point_id" {
  description = "ID of the EFS access point for Git repositories"
  value       = aws_efs_access_point.git_repos.id
}
