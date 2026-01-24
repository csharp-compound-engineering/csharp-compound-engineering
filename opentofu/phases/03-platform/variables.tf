################################################################################
# Variables — Phase 3: Platform
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
# External Secrets Operator
# ------------------------------------------------------------------------------

variable "external_secrets_enabled" {
  description = "Whether to install External Secrets Operator"
  type        = bool
  default     = true
}

variable "external_secrets_chart_version" {
  description = "Helm chart version for external-secrets"
  type        = string
  default     = "0.12.1"
}

# ------------------------------------------------------------------------------
# ExternalDNS
# ------------------------------------------------------------------------------

variable "external_dns_enabled" {
  description = "Whether to install ExternalDNS"
  type        = bool
  default     = true
}

variable "external_dns_chart_version" {
  description = "Helm chart version for external-dns"
  type        = string
  default     = "1.15.1"
}

# ------------------------------------------------------------------------------
# ArgoCD
# ------------------------------------------------------------------------------

variable "argocd_enabled" {
  description = "Whether to install ArgoCD"
  type        = bool
  default     = true
}

variable "argocd_chart_version" {
  description = "Helm chart version for argo-cd"
  type        = string
  default     = "7.8.0"
}

variable "argocd_dns_name" {
  description = "DNS name prefix for ArgoCD (becomes <name>.<internal_dns_zone>)"
  type        = string
  default     = "argocd"
}

# ------------------------------------------------------------------------------
# cert-manager
# ------------------------------------------------------------------------------

variable "cert_manager_enabled" {
  description = "Whether to install cert-manager with a self-signed internal CA"
  type        = bool
  default     = true
}

variable "cert_manager_chart_version" {
  description = "Helm chart version for cert-manager"
  type        = string
  default     = "v1.17.2"
}

# ------------------------------------------------------------------------------
# Crossplane
# ------------------------------------------------------------------------------

variable "crossplane_enabled" {
  description = "Whether to install Crossplane"
  type        = bool
  default     = true
}

variable "crossplane_chart_version" {
  description = "Helm chart version for Crossplane"
  type        = string
  default     = "2.1.0"
}

variable "crossplane_replicas" {
  description = "Number of Crossplane controller and RBAC manager replicas (use >=2 for HA)"
  type        = number
  default     = 1

  validation {
    condition     = var.crossplane_replicas >= 1
    error_message = "Crossplane replicas must be at least 1."
  }
}
