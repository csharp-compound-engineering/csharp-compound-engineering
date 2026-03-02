################################################################################
# Outputs — Phase 0: Prerequisites
################################################################################

# ------------------------------------------------------------------------------
# External Secrets Operator
# ------------------------------------------------------------------------------

output "external_secrets_role_arn" {
  description = "IAM role ARN for External Secrets Operator (Pod Identity)"
  value       = var.external_secrets_enabled ? aws_iam_role.external_secrets[0].arn : null
}

# ------------------------------------------------------------------------------
# Crossplane
# ------------------------------------------------------------------------------

output "crossplane_provider_aws_role_arn" {
  description = "IAM role ARN for Crossplane AWS provider (Pod Identity)"
  value       = var.crossplane_enabled ? aws_iam_role.crossplane_provider_aws[0].arn : null
}

# ------------------------------------------------------------------------------
# ArgoCD Secrets Manager
# ------------------------------------------------------------------------------

output "argocd_admin_password" {
  description = "Plaintext ArgoCD admin password"
  value       = var.argocd_enabled ? var.argocd_admin_password : null
}

output "argocd_secret_arn" {
  description = "ARN of the ArgoCD Secrets Manager secret"
  value       = var.argocd_enabled && var.external_secrets_enabled ? aws_secretsmanager_secret.argocd_main[0].arn : null
}

output "argocd_secret_id" {
  description = "ID of the ArgoCD Secrets Manager secret"
  value       = var.argocd_enabled && var.external_secrets_enabled ? aws_secretsmanager_secret.argocd_main[0].id : null
}

# ------------------------------------------------------------------------------
# EFS CSI Driver
# ------------------------------------------------------------------------------

output "efs_csi_driver_role_arn" {
  description = "IAM role ARN for EFS CSI driver (Pod Identity)"
  value       = var.efs_enabled ? aws_iam_role.efs_csi_driver[0].arn : ""
}
