################################################################################
# Outputs — Phase 1: Network
################################################################################

# ------------------------------------------------------------------------------
# VPC
# ------------------------------------------------------------------------------

output "vpc_id" {
  description = "ID of the VPC"
  value       = module.vpc.vpc_id
}

output "private_subnets" {
  description = "IDs of the private subnets (Lambda and data services run here)"
  value       = module.vpc.private_subnets
}

output "public_subnets" {
  description = "IDs of the public subnets (Fargate tasks with public IP assignment)"
  value       = module.vpc.public_subnets
}

# ------------------------------------------------------------------------------
# Security Groups
# ------------------------------------------------------------------------------

output "lambda_security_group_id" {
  description = "Security group ID for Lambda functions"
  value       = aws_security_group.lambda.id
}

output "fargate_security_group_id" {
  description = "Security group ID for Fargate tasks"
  value       = aws_security_group.fargate.id
}

output "neptune_security_group_id" {
  description = "Security group ID for Neptune cluster"
  value       = aws_security_group.neptune.id
}

output "opensearch_security_group_id" {
  description = "Security group ID for OpenSearch domain"
  value       = aws_security_group.opensearch.id
}

output "efs_security_group_id" {
  description = "Security group ID for EFS"
  value       = aws_security_group.efs.id
}
