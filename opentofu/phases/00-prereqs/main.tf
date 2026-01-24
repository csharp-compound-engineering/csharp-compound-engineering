################################################################################
# Phase 0: Prerequisites — IAM + Secrets Manager
#
# This phase manages AWS-only resources (IAM roles, policies, pod identity
# associations, and Secrets Manager secrets) that live outside the normal
# deploy/destroy lifecycle. This prevents soft-delete conflicts with Secrets
# Manager and keeps IAM resources stable across cluster rebuilds.
################################################################################

locals {
  common_tags = {
    OpenTofu    = "true"
    Environment = var.environment
    Cluster     = var.cluster_name
  }

  internal_dns_zone_arn = data.terraform_remote_state.network.outputs.internal_dns_zone_arn
}

################################################################################
# External Secrets Operator — IAM
################################################################################

resource "aws_iam_role" "external_secrets" {
  count = var.external_secrets_enabled ? 1 : 0

  name = "${var.cluster_name}-external-secrets"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "pods.eks.amazonaws.com"
        }
        Action = ["sts:AssumeRole", "sts:TagSession"]
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_eks_pod_identity_association" "external_secrets" {
  count = var.external_secrets_enabled ? 1 : 0

  cluster_name    = var.cluster_name
  namespace       = "external-secrets"
  service_account = "external-secrets"
  role_arn        = aws_iam_role.external_secrets[0].arn
}

resource "aws_iam_policy" "external_secrets" {
  count = var.external_secrets_enabled ? 1 : 0

  name        = "${var.cluster_name}-external-secrets"
  description = "Allow ESO to read secrets from AWS Secrets Manager"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:ListSecrets",
          "secretsmanager:DescribeSecret",
        ]
        Resource = "arn:aws:secretsmanager:${var.region}:${data.aws_caller_identity.current.account_id}:secret:${var.cluster_name}/*"
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "external_secrets" {
  count = var.external_secrets_enabled ? 1 : 0

  role       = aws_iam_role.external_secrets[0].name
  policy_arn = aws_iam_policy.external_secrets[0].arn
}

################################################################################
# ExternalDNS — IAM
################################################################################

resource "aws_iam_role" "external_dns" {
  count = var.external_dns_enabled ? 1 : 0

  name = "${var.cluster_name}-external-dns"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "pods.eks.amazonaws.com"
        }
        Action = ["sts:AssumeRole", "sts:TagSession"]
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_eks_pod_identity_association" "external_dns" {
  count = var.external_dns_enabled ? 1 : 0

  cluster_name    = var.cluster_name
  namespace       = "external-dns"
  service_account = "external-dns"
  role_arn        = aws_iam_role.external_dns[0].arn
}

resource "aws_iam_policy" "external_dns" {
  count = var.external_dns_enabled ? 1 : 0

  name        = "${var.cluster_name}-external-dns"
  description = "Allow ExternalDNS to manage Route 53 records in the private hosted zone"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "route53:ChangeResourceRecordSets",
          "route53:ListResourceRecordSets",
          "route53:ListTagsForResources",
        ]
        Resource = local.internal_dns_zone_arn
      },
      {
        Effect   = "Allow"
        Action   = "route53:ListHostedZones"
        Resource = "*"
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "external_dns" {
  count = var.external_dns_enabled ? 1 : 0

  role       = aws_iam_role.external_dns[0].name
  policy_arn = aws_iam_policy.external_dns[0].arn
}

################################################################################
# Crossplane — IAM
################################################################################

resource "aws_iam_role" "crossplane_provider_aws" {
  count = var.crossplane_enabled ? 1 : 0

  name = "${var.cluster_name}-crossplane-provider-aws"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "pods.eks.amazonaws.com"
        }
        Action = ["sts:AssumeRole", "sts:TagSession"]
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_eks_pod_identity_association" "crossplane_provider_aws" {
  count = var.crossplane_enabled ? 1 : 0

  cluster_name    = var.cluster_name
  namespace       = "crossplane-system"
  service_account = "provider-opentofu"
  role_arn        = aws_iam_role.crossplane_provider_aws[0].arn
}

resource "aws_iam_role_policy_attachment" "crossplane_provider_aws" {
  count = var.crossplane_enabled && var.crossplane_provider_aws_policy_arn != "" ? 1 : 0

  role       = aws_iam_role.crossplane_provider_aws[0].name
  policy_arn = var.crossplane_provider_aws_policy_arn
}

################################################################################
# ArgoCD — AWS Secrets Manager
################################################################################

resource "aws_secretsmanager_secret" "argocd_main" {
  count = var.argocd_enabled && var.external_secrets_enabled ? 1 : 0

  name                    = "${var.cluster_name}/argocd/argocd-secret"
  description             = "ArgoCD server credentials (admin password + session key)"
  recovery_window_in_days = 0

  tags = local.common_tags
}

resource "aws_secretsmanager_secret_version" "argocd_main" {
  count = var.argocd_enabled && var.external_secrets_enabled ? 1 : 0

  secret_id = aws_secretsmanager_secret.argocd_main[0].id

  secret_string = jsonencode({
    "admin.password"      = var.argocd_admin_password_bcrypt
    "admin.passwordMtime" = formatdate("YYYY-MM-DD'T'HH:mm:ss'Z'", timestamp())
    "server.secretkey"    = var.argocd_server_secret_key
  })

  lifecycle {
    ignore_changes = [secret_string]
  }
}
