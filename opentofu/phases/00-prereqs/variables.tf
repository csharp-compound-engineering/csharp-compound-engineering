################################################################################
# Variables — Phase 0: Prerequisites
################################################################################

# ------------------------------------------------------------------------------
# General
# ------------------------------------------------------------------------------

variable "region" {
  description = "AWS region to deploy into"
  type        = string
  default     = "us-east-2"
}

variable "cluster_name" {
  description = "Name of the EKS cluster (used to name all associated resources)"
  type        = string

  validation {
    condition     = can(regex("^[a-zA-Z][a-zA-Z0-9-]*$", var.cluster_name))
    error_message = "Cluster name must start with a letter and contain only alphanumeric characters and hyphens."
  }
}

variable "environment" {
  description = "Deployment environment (used for resource tagging)"
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be one of: dev, staging, production."
  }
}

# ------------------------------------------------------------------------------
# DNS
# ------------------------------------------------------------------------------

variable "internal_dns_zone" {
  description = "Domain name for the Route 53 private hosted zone (e.g. internal.compound-docs.com)"
  type        = string
}

# ------------------------------------------------------------------------------
# Feature Toggles
# ------------------------------------------------------------------------------

variable "external_secrets_enabled" {
  description = "Whether to create IAM resources for External Secrets Operator"
  type        = bool
  default     = true
}

variable "external_dns_enabled" {
  description = "Whether to create IAM resources for ExternalDNS"
  type        = bool
  default     = true
}

variable "crossplane_enabled" {
  description = "Whether to create IAM resources for Crossplane"
  type        = bool
  default     = true
}

variable "crossplane_provider_aws_policy_arn" {
  description = "ARN of an additional IAM policy to attach to the Crossplane AWS provider role. The default compound-docs policy is always attached."
  type        = string
  default     = ""
}

# ------------------------------------------------------------------------------
# ArgoCD Secrets
# ------------------------------------------------------------------------------

variable "argocd_enabled" {
  description = "Whether to create ArgoCD secrets in AWS Secrets Manager"
  type        = bool
  default     = true
}

variable "argocd_admin_password_bcrypt" {
  description = "Bcrypt hash of the ArgoCD admin password. Generate with: htpasswd -nbBC 10 '' 'your-password' | tr -d ':'"
  type        = string
  default     = ""
  sensitive   = true
}

variable "argocd_server_secret_key" {
  description = "HMAC key for ArgoCD JWT session tokens. Generate with: openssl rand -hex 32"
  type        = string
  default     = ""
  sensitive   = true
}
