################################################################################
# Variables — Phase 2: Cluster
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
# EKS Cluster
# ------------------------------------------------------------------------------

variable "cluster_version" {
  description = "Kubernetes version for the EKS cluster"
  type        = string
  default     = "1.31"
}

variable "cluster_endpoint_public_access" {
  description = "Whether the EKS API server endpoint is publicly accessible"
  type        = bool
  default     = false
}

variable "cluster_endpoint_private_access" {
  description = "Whether the EKS API server endpoint is accessible from within the VPC"
  type        = bool
  default     = true
}

# ------------------------------------------------------------------------------
# Node Groups
# ------------------------------------------------------------------------------

variable "node_instance_types" {
  description = "EC2 instance types for the application node group"
  type        = list(string)
  default     = ["t3.medium"]
}

variable "node_min_size" {
  description = "Minimum number of nodes in the application node group"
  type        = number
  default     = 2

  validation {
    condition     = var.node_min_size >= 0
    error_message = "Minimum node size must be non-negative."
  }
}

variable "node_max_size" {
  description = "Maximum number of nodes in the application node group"
  type        = number
  default     = 6

  validation {
    condition     = var.node_max_size >= 1
    error_message = "Maximum node size must be at least 1."
  }
}

variable "node_desired_size" {
  description = "Desired number of nodes in the application node group"
  type        = number
  default     = 2

  validation {
    condition     = var.node_desired_size >= 0
    error_message = "Desired node size must be non-negative."
  }
}

# ------------------------------------------------------------------------------
# Pod Identity Associations
# ------------------------------------------------------------------------------

variable "external_secrets_enabled" {
  description = "Whether to create EKS Pod Identity association for External Secrets Operator"
  type        = bool
  default     = true
}

variable "external_dns_enabled" {
  description = "Whether to create EKS Pod Identity association for ExternalDNS"
  type        = bool
  default     = true
}

variable "crossplane_enabled" {
  description = "Whether to create EKS Pod Identity association for Crossplane"
  type        = bool
  default     = true
}

variable "efs_enabled" {
  description = "Whether to create EFS resources and EKS Pod Identity association for EFS CSI driver"
  type        = bool
  default     = true
}
