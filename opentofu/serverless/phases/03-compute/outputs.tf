################################################################################
# Outputs — Phase 3: Compute
################################################################################

# ------------------------------------------------------------------------------
# Lambda
# ------------------------------------------------------------------------------

output "lambda_function_name" {
  description = "Name of the Lambda function"
  value       = aws_lambda_function.mcp_server.function_name
}

output "lambda_function_url" {
  description = "Function URL for the Lambda MCP server"
  value       = aws_lambda_function_url.mcp_server.function_url
}

output "lambda_artifacts_bucket" {
  description = "S3 bucket for Lambda deployment artifacts"
  value       = aws_s3_bucket.lambda_artifacts.id
}

# ------------------------------------------------------------------------------
# ECS / Fargate
# ------------------------------------------------------------------------------

output "ecs_cluster_name" {
  description = "Name of the ECS cluster"
  value       = aws_ecs_cluster.main.name
}

output "ecs_cluster_arn" {
  description = "ARN of the ECS cluster"
  value       = aws_ecs_cluster.main.arn
}

output "gitsync_task_definition_arn" {
  description = "ARN of the GitSync ECS task definition"
  value       = aws_ecs_task_definition.gitsync.arn
}

# ------------------------------------------------------------------------------
# EventBridge
# ------------------------------------------------------------------------------

output "gitsync_schedule_arn" {
  description = "ARN of the EventBridge scheduler for GitSync"
  value       = aws_scheduler_schedule.gitsync.arn
}
