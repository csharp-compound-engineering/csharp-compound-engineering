################################################################################
# Variables — Neptune Module
################################################################################

variable "name_prefix" {
  description = "Prefix for all Neptune resource names (e.g. 'compound-docs-dev')"
  type        = string
}

variable "engine_version" {
  description = "Neptune engine version"
  type        = string
  default     = "1.2.0.1"
}

variable "instance_class" {
  description = "Neptune instance class (e.g. db.t4g.medium, db.r6g.large)"
  type        = string
  default     = "db.t4g.medium"
}

variable "vpc_id" {
  description = "VPC ID where Neptune is deployed (used for documentation and validation)"
  type        = string
}

variable "subnet_ids" {
  description = "List of private subnet IDs for the Neptune subnet group"
  type        = list(string)
}

variable "security_group_id" {
  description = "Security group ID to attach to the Neptune cluster"
  type        = string
}

variable "create_service_linked_role" {
  description = "Whether to create the AWSServiceRoleForRDS service-linked role (only needed once per account)"
  type        = bool
  default     = false
}

variable "tags" {
  description = "Tags to apply to all Neptune resources"
  type        = map(string)
  default     = {}
}
