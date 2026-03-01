################################################################################
# Variables — Phase 1: Network
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
# Network
# ------------------------------------------------------------------------------

variable "vpc_cidr" {
  description = "CIDR block for the VPC"
  type        = string
  default     = "10.0.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpc_cidr, 0))
    error_message = "VPC CIDR must be a valid IPv4 CIDR block."
  }
}

variable "single_nat_gateway" {
  description = "Use a single shared NAT Gateway (cost-saving for dev). Set to false for one NAT Gateway per AZ (production HA)."
  type        = bool
  default     = true
}

# ------------------------------------------------------------------------------
# DNS
# ------------------------------------------------------------------------------

variable "internal_dns_zone" {
  description = "Domain name for the Route 53 private hosted zone (e.g. internal.compound-docs.com)"
  type        = string
}

# ------------------------------------------------------------------------------
# VPN
# ------------------------------------------------------------------------------

variable "vpn_enabled" {
  description = "Whether to create the AWS Client VPN endpoint"
  type        = bool
  default     = true
}

variable "vpn_client_cidr" {
  description = "CIDR block for VPN client IP allocation (must not overlap VPC CIDR)"
  type        = string
  default     = "172.16.0.0/16"

  validation {
    condition     = can(cidrhost(var.vpn_client_cidr, 0))
    error_message = "VPN client CIDR must be a valid IPv4 CIDR block."
  }
}
