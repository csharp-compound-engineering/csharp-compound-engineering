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

# ------------------------------------------------------------------------------
# GitSync
# ------------------------------------------------------------------------------

variable "gitsync_image_digest" {
  description = "GitSync container image digest — updated by CI"
  type        = string
  default     = "sha256:c6f82989522a49e749c6be96092dec0a8f5e1b437b768f1073eda90d7dfd4cf8"
}

variable "gitsync_schedule" {
  description = "EventBridge schedule expression for GitSync runs"
  type        = string
  default     = "rate(6 hours)"
}
