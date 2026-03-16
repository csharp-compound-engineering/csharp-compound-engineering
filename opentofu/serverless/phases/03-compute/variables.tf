################################################################################
# Variables — Phase 3: Compute
################################################################################

# ------------------------------------------------------------------------------
# General
# ------------------------------------------------------------------------------

variable "region" {
  description = "AWS region to deploy into"
  type        = string
  default     = "us-east-2"
}

variable "stack_name" {
  description = "Name of the serverless stack (used to name all associated resources)"
  type        = string

  validation {
    condition     = can(regex("^[a-zA-Z][a-zA-Z0-9-]*$", var.stack_name))
    error_message = "Stack name must start with a letter and contain only alphanumeric characters and hyphens."
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
# Lambda
# ------------------------------------------------------------------------------

variable "lambda_memory_size" {
  description = "Memory size for the Lambda function (in MB)"
  type        = number
  default     = 512
}

variable "lambda_timeout" {
  description = "Timeout for the Lambda function (in seconds)"
  type        = number
  default     = 30
}

variable "lambda_zip_version" {
  description = "Version of the Lambda deployment ZIP (matches GitHub release) — updated by CI"
  type        = string
  default     = "5.1.2"
}

# ------------------------------------------------------------------------------
# GitSync
# ------------------------------------------------------------------------------

variable "gitsync_image_digest" {
  description = "GitSync container image digest — updated by CI"
  type        = string
  default     = "sha256:831979de4ad9fec6dd099c8090c8506f789b5271f6d4efd27187a2f21ae6e983"
}

variable "gitsync_schedule" {
  description = "EventBridge schedule expression for GitSync runs"
  type        = string
  default     = "rate(6 hours)"
}
