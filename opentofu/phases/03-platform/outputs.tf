################################################################################
# Outputs — Phase 3: Platform
################################################################################

# ------------------------------------------------------------------------------
# ArgoCD
# ------------------------------------------------------------------------------

output "argocd_url" {
  description = "URL to access ArgoCD (requires VPN connection)"
  value       = var.argocd_enabled ? "https://${var.argocd_dns_name}.${var.internal_dns_zone}" : null
}

output "argocd_namespace" {
  description = "Kubernetes namespace where ArgoCD is installed"
  value       = var.argocd_enabled ? "argocd" : null
}

output "argocd_admin_password_cmd" {
  description = "Command to retrieve the ArgoCD admin password"
  value = var.argocd_enabled ? (
    var.external_secrets_enabled
    ? "kubectl -n argocd get secret argocd-secret -o jsonpath=\"{.data.admin\\.password}\" | base64 -d; echo"
    : "kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath=\"{.data.password}\" | base64 -d; echo"
  ) : null
}

# ------------------------------------------------------------------------------
# Crossplane
# ------------------------------------------------------------------------------

output "crossplane_namespace" {
  description = "Kubernetes namespace where Crossplane is installed"
  value       = var.crossplane_enabled ? "crossplane-system" : null
}

output "crossplane_provider_aws_role_arn" {
  description = "IAM role ARN for Crossplane AWS provider (Pod Identity)"
  value       = data.terraform_remote_state.prereqs.outputs.crossplane_provider_aws_role_arn
}

# ------------------------------------------------------------------------------
# cert-manager
# ------------------------------------------------------------------------------

output "cert_manager_ca_cert_cmd" {
  description = "Command to extract the internal CA certificate to a PEM file"
  value       = var.cert_manager_enabled ? "kubectl -n cert-manager get secret internal-ca-key-pair -o jsonpath=\"{.data.tls\\.crt}\" | base64 -d > internal-ca.crt" : null
}

output "cert_manager_ca_trust_macos_cmd" {
  description = "Command to trust the internal CA on macOS (requires sudo)"
  value       = var.cert_manager_enabled ? "sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain internal-ca.crt" : null
}
