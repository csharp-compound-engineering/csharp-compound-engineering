################################################################################
# Phase 0: Prerequisites — IAM + Secrets Manager
#
# This phase manages AWS-only resources (IAM roles, policies, and Secrets
# Manager secrets) that live outside the normal
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
        Resource = "arn:aws:secretsmanager:${var.region}:${data.aws_caller_identity.current.account_id}:secret:*"
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

resource "aws_iam_policy" "crossplane_provider_aws" {
  count = var.crossplane_enabled ? 1 : 0

  name        = "${var.cluster_name}-crossplane-provider-aws"
  description = "Permissions for Crossplane provider-opentofu to provision compound-docs infrastructure"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "Neptune"
        Effect = "Allow"
        Action = [
          "rds:CreateDBSubnetGroup",
          "rds:DeleteDBSubnetGroup",
          "rds:DescribeDBSubnetGroups",
          "rds:CreateDBClusterParameterGroup",
          "rds:DeleteDBClusterParameterGroup",
          "rds:DescribeDBClusterParameterGroups",
          "rds:DescribeDBClusterParameters",
          "rds:ModifyDBClusterParameterGroup",
          "rds:CreateDBCluster",
          "rds:DeleteDBCluster",
          "rds:DescribeDBClusters",
          "rds:ModifyDBCluster",
          "rds:CreateDBInstance",
          "rds:DeleteDBInstance",
          "rds:DescribeDBInstances",
          "rds:AddTagsToResource",
          "rds:RemoveTagsFromResource",
          "rds:ListTagsForResource",
          "rds:DescribeGlobalClusters",
        ]
        Resource = "*"
      },
      {
        Sid    = "OpenSearchServerless"
        Effect = "Allow"
        Action = [
          "aoss:CreateSecurityPolicy",
          "aoss:GetSecurityPolicy",
          "aoss:UpdateSecurityPolicy",
          "aoss:DeleteSecurityPolicy",
          "aoss:CreateAccessPolicy",
          "aoss:GetAccessPolicy",
          "aoss:UpdateAccessPolicy",
          "aoss:DeleteAccessPolicy",
          "aoss:CreateCollection",
          "aoss:DeleteCollection",
          "aoss:BatchGetCollection",
          "aoss:CreateVpcEndpoint",
          "aoss:DeleteVpcEndpoint",
          "aoss:UpdateVpcEndpoint",
          "aoss:BatchGetVpcEndpoint",
          "aoss:TagResource",
          "aoss:UntagResource",
          "aoss:ListTagsForResource",
        ]
        Resource = "*"
      },
      {
        Sid    = "IAM"
        Effect = "Allow"
        Action = [
          "iam:CreateRole",
          "iam:DeleteRole",
          "iam:GetRole",
          "iam:TagRole",
          "iam:UntagRole",
          "iam:ListRolePolicies",
          "iam:ListAttachedRolePolicies",
          "iam:ListInstanceProfilesForRole",
          "iam:PutRolePolicy",
          "iam:DeleteRolePolicy",
          "iam:GetRolePolicy",
          "iam:PassRole",
        ]
        Resource = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/*"
      },
      {
        Sid    = "IAMServiceLinkedRole"
        Effect = "Allow"
        Action = [
          "iam:CreateServiceLinkedRole",
          "iam:DeleteServiceLinkedRole",
          "iam:GetServiceLinkedRoleDeletionStatus",
        ]
        Resource = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/aws-service-role/*"
      },
      {
        Sid    = "EKSPodIdentity"
        Effect = "Allow"
        Action = [
          "eks:CreatePodIdentityAssociation",
          "eks:DeletePodIdentityAssociation",
          "eks:DescribePodIdentityAssociation",
          "eks:ListPodIdentityAssociations",
        ]
        Resource = "*"
      },
      {
        Sid    = "SecretsManager"
        Effect = "Allow"
        Action = [
          "secretsmanager:CreateSecret",
          "secretsmanager:DeleteSecret",
          "secretsmanager:DescribeSecret",
          "secretsmanager:GetSecretValue",
          "secretsmanager:GetResourcePolicy",
          "secretsmanager:PutSecretValue",
          "secretsmanager:TagResource",
          "secretsmanager:UntagResource",
        ]
        Resource = "arn:aws:secretsmanager:${var.region}:${data.aws_caller_identity.current.account_id}:secret:*"
      },
      {
        Sid    = "STSAndEC2ReadOnly"
        Effect = "Allow"
        Action = [
          "sts:GetCallerIdentity",
        ]
        Resource = "*"
      },
      {
        Sid    = "EC2VpcEndpoint"
        Effect = "Allow"
        Action = [
          "ec2:CreateVpcEndpoint",
          "ec2:DeleteVpcEndpoints",
          "ec2:DescribeVpcEndpoints",
          "ec2:ModifyVpcEndpoint",
          "ec2:DescribeVpcs",
          "ec2:DescribeSubnets",
          "ec2:DescribeSecurityGroups",
          "ec2:DescribeNetworkInterfaces",
          "ec2:CreateTags",
        ]
        Resource = "*"
      },
      {
        Sid    = "Route53ForAOSSVpcEndpoint"
        Effect = "Allow"
        Action = [
          "route53:CreateHostedZone",
          "route53:DeleteHostedZone",
          "route53:GetChange",
          "route53:AssociateVPCWithHostedZone",
          "route53:DisassociateVPCFromHostedZone",
          "route53:ListHostedZonesByVPC",
          "route53:ListHostedZonesByName",
          "route53:ChangeResourceRecordSets",
          "route53:GetHostedZone",
          "route53:ListResourceRecordSets",
        ]
        Resource = "*"
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "crossplane_provider_aws_default" {
  count = var.crossplane_enabled ? 1 : 0

  role       = aws_iam_role.crossplane_provider_aws[0].name
  policy_arn = aws_iam_policy.crossplane_provider_aws[0].arn
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
    "admin.passwordMtime" = "2026-01-01T00:00:00Z"
    "server.secretkey"    = var.argocd_server_secret_key
  })
}

################################################################################
# EFS CSI Driver — IAM
################################################################################

resource "aws_iam_role" "efs_csi_driver" {
  count = var.efs_enabled ? 1 : 0

  name = "${var.cluster_name}-efs-csi-driver"

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

resource "aws_iam_policy" "efs_csi_driver" {
  count = var.efs_enabled ? 1 : 0

  name        = "${var.cluster_name}-efs-csi-driver"
  description = "Allow EFS CSI driver to manage EFS access points and mount targets"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "elasticfilesystem:DescribeAccessPoints",
          "elasticfilesystem:DescribeFileSystems",
          "elasticfilesystem:DescribeMountTargets",
          "elasticfilesystem:CreateAccessPoint",
          "elasticfilesystem:DeleteAccessPoint",
          "elasticfilesystem:ClientMount",
          "elasticfilesystem:ClientWrite",
          "elasticfilesystem:ClientRootAccess",
        ]
        Resource = "*"
      }
    ]
  })

  tags = local.common_tags
}

resource "aws_iam_role_policy_attachment" "efs_csi_driver" {
  count = var.efs_enabled ? 1 : 0

  role       = aws_iam_role.efs_csi_driver[0].name
  policy_arn = aws_iam_policy.efs_csi_driver[0].arn
}
