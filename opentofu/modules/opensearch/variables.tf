################################################################################
# Variables — OpenSearch Module
################################################################################

variable "domain_name" {
  description = "Name of the OpenSearch domain (lowercase alphanumeric and hyphens, 3-28 characters)"
  type        = string

  validation {
    condition     = can(regex("^[a-z][a-z0-9-]{2,27}$", var.domain_name))
    error_message = "Domain name must be 3-28 characters, start with a lowercase letter, and contain only lowercase alphanumeric characters and hyphens."
  }
}

variable "engine_version" {
  description = "OpenSearch engine version"
  type        = string
  default     = "OpenSearch_2.17"
}

variable "instance_type" {
  description = "OpenSearch instance type (e.g. t3.small.search, r6g.large.search)"
  type        = string
  default     = "t3.small.search"
}

variable "instance_count" {
  description = "Number of data nodes in the OpenSearch cluster"
  type        = number
  default     = 1
}

variable "ebs_volume_size" {
  description = "Size of EBS volumes attached to data nodes (in GB)"
  type        = number
  default     = 20
}

variable "vpc_id" {
  description = "VPC ID where OpenSearch is deployed"
  type        = string
}

variable "subnet_ids" {
  description = "List of subnet IDs for the OpenSearch domain (first subnet used for single-AZ deployments)"
  type        = list(string)
}

variable "security_group_id" {
  description = "Security group ID to attach to the OpenSearch domain"
  type        = string
}

variable "tags" {
  description = "Tags to apply to all OpenSearch resources"
  type        = map(string)
  default     = {}
}
