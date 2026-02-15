variable "name_prefix" {
  type    = string
  default = "compound-docs"
}

variable "cluster_name" {
  type = string
}

variable "namespace" {
  type    = string
  default = "default"
}

variable "service_account_name" {
  type    = string
  default = "compound-docs"
}

variable "neptune_cluster_resource_id" {
  type    = string
  default = "*"
}

variable "write_to_secrets_manager" {
  type    = bool
  default = false
}

variable "secrets_manager_prefix" {
  type    = string
  default = ""
}

variable "eso_service_account_name" {
  type    = string
  default = ""
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

data "aws_iam_policy_document" "assume_role" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole", "sts:TagSession"]

    principals {
      type        = "Service"
      identifiers = ["pods.eks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "main" {
  name               = "${var.name_prefix}-pod-identity"
  assume_role_policy = data.aws_iam_policy_document.assume_role.json

  tags = {
    Name = "${var.name_prefix}-pod-identity"
  }
}

resource "aws_eks_pod_identity_association" "main" {
  cluster_name    = var.cluster_name
  namespace       = var.namespace
  service_account = var.service_account_name
  role_arn        = aws_iam_role.main.arn
}

data "aws_iam_policy_document" "neptune" {
  statement {
    effect = "Allow"
    actions = [
      "neptune-db:connect",
      "neptune-db:ReadDataViaQuery",
      "neptune-db:WriteDataViaQuery",
    ]
    resources = [
      "arn:aws:neptune-db:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:${var.neptune_cluster_resource_id}/*"
    ]
  }
}

data "aws_iam_policy_document" "bedrock" {
  statement {
    effect = "Allow"
    actions = [
      "bedrock:InvokeModel",
      "bedrock:InvokeModelWithResponseStream",
    ]
    resources = ["*"]
  }
}

resource "aws_iam_role_policy" "neptune" {
  name   = "neptune-access"
  role   = aws_iam_role.main.id
  policy = data.aws_iam_policy_document.neptune.json
}

resource "aws_iam_role_policy" "bedrock" {
  name   = "bedrock-access"
  role   = aws_iam_role.main.id
  policy = data.aws_iam_policy_document.bedrock.json
}

# --- ESO reader Pod Identity role ---
data "aws_iam_policy_document" "eso_assume_role" {
  count = var.write_to_secrets_manager ? 1 : 0

  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole", "sts:TagSession"]

    principals {
      type        = "Service"
      identifiers = ["pods.eks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "eso_reader" {
  count              = var.write_to_secrets_manager ? 1 : 0
  name               = "${var.name_prefix}-eso-reader"
  assume_role_policy = data.aws_iam_policy_document.eso_assume_role[0].json

  tags = {
    Name = "${var.name_prefix}-eso-reader"
  }
}

resource "aws_eks_pod_identity_association" "eso_reader" {
  count = var.write_to_secrets_manager ? 1 : 0

  cluster_name    = var.cluster_name
  namespace       = var.namespace
  service_account = var.eso_service_account_name
  role_arn        = aws_iam_role.eso_reader[0].arn
}

data "aws_iam_policy_document" "eso_secrets_manager" {
  count = var.write_to_secrets_manager ? 1 : 0

  statement {
    effect = "Allow"
    actions = [
      "secretsmanager:GetSecretValue",
      "secretsmanager:DescribeSecret",
    ]
    resources = [
      "arn:aws:secretsmanager:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:secret:${var.secrets_manager_prefix}/*"
    ]
  }
}

resource "aws_iam_role_policy" "eso_secrets_manager" {
  count  = var.write_to_secrets_manager ? 1 : 0
  name   = "secrets-manager-read"
  role   = aws_iam_role.eso_reader[0].id
  policy = data.aws_iam_policy_document.eso_secrets_manager[0].json
}

# --- Write IAM outputs to Secrets Manager ---
resource "aws_secretsmanager_secret" "iam" {
  count                   = var.write_to_secrets_manager ? 1 : 0
  name                    = "${var.secrets_manager_prefix}/iam"
  recovery_window_in_days = 0

  tags = {
    Name = "${var.name_prefix}-iam-secret"
  }
}

resource "aws_secretsmanager_secret_version" "iam" {
  count     = var.write_to_secrets_manager ? 1 : 0
  secret_id = aws_secretsmanager_secret.iam[0].id
  secret_string = jsonencode({
    role_arn     = aws_iam_role.main.arn
    eso_role_arn = aws_iam_role.eso_reader[0].arn
  })
}

output "role_arn" {
  value = aws_iam_role.main.arn
}

output "eso_role_arn" {
  value = var.write_to_secrets_manager ? aws_iam_role.eso_reader[0].arn : ""
}
