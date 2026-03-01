################################################################################
# Outputs — Phase 0: Prerequisites
################################################################################

# ------------------------------------------------------------------------------
# Account
# ------------------------------------------------------------------------------

output "account_id" {
  description = "AWS account ID"
  value       = data.aws_caller_identity.current.account_id
}

# ------------------------------------------------------------------------------
# Lambda Execution Role
# ------------------------------------------------------------------------------

output "lambda_execution_role_arn" {
  description = "IAM role ARN for Lambda execution"
  value       = aws_iam_role.lambda_execution.arn
}

# ------------------------------------------------------------------------------
# Fargate Task Execution Role
# ------------------------------------------------------------------------------

output "fargate_task_execution_role_arn" {
  description = "IAM role ARN for Fargate task execution (ECS agent)"
  value       = aws_iam_role.fargate_task_execution.arn
}

# ------------------------------------------------------------------------------
# Fargate Task Role
# ------------------------------------------------------------------------------

output "fargate_task_role_arn" {
  description = "IAM role ARN for Fargate task (application-level permissions)"
  value       = aws_iam_role.fargate_task.arn
}

# ------------------------------------------------------------------------------
# Secrets Manager
# ------------------------------------------------------------------------------

output "api_keys_secret_arn" {
  description = "ARN of the API keys Secrets Manager secret"
  value       = aws_secretsmanager_secret.api_keys.arn
}

output "api_keys_secret_id" {
  description = "ID of the API keys Secrets Manager secret"
  value       = aws_secretsmanager_secret.api_keys.id
}

output "neptune_secret_arn" {
  description = "ARN of the Neptune Secrets Manager secret"
  value       = aws_secretsmanager_secret.neptune.arn
}

output "neptune_secret_id" {
  description = "ID of the Neptune Secrets Manager secret"
  value       = aws_secretsmanager_secret.neptune.id
}

output "opensearch_secret_arn" {
  description = "ARN of the OpenSearch Secrets Manager secret"
  value       = aws_secretsmanager_secret.opensearch.arn
}

output "opensearch_secret_id" {
  description = "ID of the OpenSearch Secrets Manager secret"
  value       = aws_secretsmanager_secret.opensearch.id
}
