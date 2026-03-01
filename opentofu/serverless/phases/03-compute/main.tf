################################################################################
# Phase 3: Compute — Lambda + Fargate + EventBridge (Serverless)
################################################################################

locals {
  common_tags = {
    OpenTofu    = "true"
    Environment = var.environment
    Stack       = var.stack_name
  }

  gitsync_image = "ghcr.io/csharp-compound-engineering/csharp-compound-engineering/gitsync-job@${var.gitsync_image_digest}"
}

################################################################################
# Lambda — S3 Deployment Artifacts
################################################################################

resource "aws_s3_bucket" "lambda_artifacts" {
  bucket = "${var.stack_name}-lambda-artifacts-${data.terraform_remote_state.prereqs.outputs.account_id}"

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-lambda-artifacts"
  })
}

resource "aws_s3_bucket_versioning" "lambda_artifacts" {
  bucket = aws_s3_bucket.lambda_artifacts.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "lambda_artifacts" {
  bucket = aws_s3_bucket.lambda_artifacts.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "lambda_artifacts" {
  bucket = aws_s3_bucket.lambda_artifacts.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

################################################################################
# Lambda Function
################################################################################

resource "aws_lambda_function" "mcp_server" {
  function_name = "${var.stack_name}-mcp-server"
  role          = data.terraform_remote_state.prereqs.outputs.lambda_execution_role_arn
  runtime       = "dotnet10"
  handler       = "CompoundDocs.McpServer"
  memory_size   = var.lambda_memory_size
  timeout       = var.lambda_timeout

  s3_bucket = aws_s3_bucket.lambda_artifacts.id
  s3_key    = "mcp-server/latest.zip"

  vpc_config {
    subnet_ids         = data.terraform_remote_state.network.outputs.private_subnets
    security_group_ids = [data.terraform_remote_state.network.outputs.lambda_security_group_id]
  }

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT                   = "Production"
      CompoundDocs__Neptune__Endpoint           = data.terraform_remote_state.data.outputs.neptune_endpoint
      CompoundDocs__OpenSearch__CollectionEndpoint = data.terraform_remote_state.data.outputs.opensearch_endpoint
      Authentication__ApiKeys                   = data.aws_secretsmanager_secret_version.api_keys.secret_string
    }
  }

  lifecycle {
    ignore_changes = [s3_key, s3_object_version]
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-mcp-server"
  })
}

################################################################################
# Lambda Function URL
################################################################################

resource "aws_lambda_function_url" "mcp_server" {
  function_name      = aws_lambda_function.mcp_server.function_name
  authorization_type = "NONE"
  invoke_mode        = "BUFFERED"

  cors {
    allow_methods = ["POST"]
    allow_headers = ["Content-Type", "X-API-Key", "Mcp-Session-Id"]
    allow_origins = ["*"]
    max_age       = 3600
  }
}

################################################################################
# Lambda — CloudWatch Log Group
################################################################################

resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${aws_lambda_function.mcp_server.function_name}"
  retention_in_days = 30

  tags = local.common_tags
}

################################################################################
# ECS Cluster (Fargate)
################################################################################

resource "aws_ecs_cluster" "main" {
  name = "${var.stack_name}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = local.common_tags
}

################################################################################
# ECS Task Definition — GitSync
################################################################################

resource "aws_ecs_task_definition" "gitsync" {
  family                   = "${var.stack_name}-gitsync"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = data.terraform_remote_state.prereqs.outputs.fargate_task_execution_role_arn
  task_role_arn            = data.terraform_remote_state.prereqs.outputs.fargate_task_role_arn

  container_definitions = jsonencode([
    {
      name      = "gitsync"
      image     = local.gitsync_image
      essential = true

      environment = [
        {
          name  = "CompoundDocs__Neptune__Endpoint"
          value = data.terraform_remote_state.data.outputs.neptune_endpoint
        },
        {
          name  = "CompoundDocs__OpenSearch__CollectionEndpoint"
          value = data.terraform_remote_state.data.outputs.opensearch_endpoint
        }
      ]

      mountPoints = [
        {
          sourceVolume  = "git-repos"
          containerPath = "/data/repos"
          readOnly      = false
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.gitsync.name
          "awslogs-region"        = var.region
          "awslogs-stream-prefix" = "gitsync"
        }
      }
    }
  ])

  volume {
    name = "git-repos"

    efs_volume_configuration {
      file_system_id     = data.terraform_remote_state.data.outputs.efs_filesystem_id
      transit_encryption = "ENABLED"

      authorization_config {
        access_point_id = data.terraform_remote_state.data.outputs.efs_access_point_id
        iam             = "ENABLED"
      }
    }
  }

  tags = local.common_tags
}

resource "aws_cloudwatch_log_group" "gitsync" {
  name              = "/aws/ecs/${var.stack_name}-gitsync"
  retention_in_days = 30

  tags = local.common_tags
}

################################################################################
# EventBridge Scheduler — GitSync
################################################################################

resource "aws_iam_role" "scheduler" {
  name = "${var.stack_name}-scheduler"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "scheduler.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_policy" "scheduler" {
  name        = "${var.stack_name}-scheduler"
  description = "Allow EventBridge Scheduler to run ECS tasks and pass roles"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = "ecs:RunTask"
        Resource = aws_ecs_task_definition.gitsync.arn
      },
      {
        Effect = "Allow"
        Action = "iam:PassRole"
        Resource = [
          data.terraform_remote_state.prereqs.outputs.fargate_task_execution_role_arn,
          data.terraform_remote_state.prereqs.outputs.fargate_task_role_arn,
        ]
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "scheduler" {
  role       = aws_iam_role.scheduler.name
  policy_arn = aws_iam_policy.scheduler.arn
}

resource "aws_scheduler_schedule" "gitsync" {
  name       = "${var.stack_name}-gitsync"
  group_name = "default"

  flexible_time_window {
    mode = "OFF"
  }

  schedule_expression = var.gitsync_schedule

  target {
    arn      = aws_ecs_cluster.main.arn
    role_arn = aws_iam_role.scheduler.arn

    ecs_parameters {
      task_definition_arn = aws_ecs_task_definition.gitsync.arn
      launch_type         = "FARGATE"

      network_configuration {
        subnets          = data.terraform_remote_state.network.outputs.public_subnets
        security_groups  = [data.terraform_remote_state.network.outputs.fargate_security_group_id]
        assign_public_ip = true
      }
    }
  }

  tags = local.common_tags
}
