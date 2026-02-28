################################################################################
# Outputs — Phase 2: Cluster
################################################################################

output "cluster_name" {
  description = "Name of the EKS cluster"
  value       = module.eks.cluster_name
}

output "cluster_endpoint" {
  description = "Endpoint URL for the EKS Kubernetes API server"
  value       = module.eks.cluster_endpoint
}

output "cluster_certificate_authority_data" {
  description = "Base64-encoded certificate data for cluster communication"
  value       = module.eks.cluster_certificate_authority_data
  sensitive   = true
}

output "configure_kubectl" {
  description = "Command to configure kubectl for this cluster"
  value       = "aws eks update-kubeconfig --region ${var.region} --name ${module.eks.cluster_name}"
}

# ------------------------------------------------------------------------------
# EFS
# ------------------------------------------------------------------------------

output "efs_filesystem_id" {
  description = "ID of the EFS file system for git repository storage"
  value       = var.efs_enabled ? aws_efs_file_system.git_repos[0].id : null
}

output "efs_access_point_id" {
  description = "ID of the EFS access point for git repository storage"
  value       = var.efs_enabled ? aws_efs_access_point.git_repos[0].id : null
}
