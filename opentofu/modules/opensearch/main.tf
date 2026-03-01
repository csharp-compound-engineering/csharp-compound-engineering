################################################################################
# OpenSearch Module — Provisioned Domain
################################################################################

# ------------------------------------------------------------------------------
# Data Sources
# ------------------------------------------------------------------------------

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

# ------------------------------------------------------------------------------
# OpenSearch Domain
# ------------------------------------------------------------------------------

resource "aws_opensearch_domain" "main" {
  domain_name    = var.domain_name
  engine_version = var.engine_version

  cluster_config {
    instance_type  = var.instance_type
    instance_count = var.instance_count
  }

  ebs_options {
    ebs_enabled = true
    volume_type = "gp3"
    volume_size = var.ebs_volume_size
  }

  encrypt_at_rest {
    enabled = true
  }

  node_to_node_encryption {
    enabled = true
  }

  domain_endpoint_options {
    enforce_https       = true
    tls_security_policy = "Policy-Min-TLS-1-2-PFS-2023-10"
  }

  advanced_security_options {
    enabled                        = true
    anonymous_auth_enabled         = false
    internal_user_database_enabled = false
  }

  vpc_options {
    subnet_ids         = [var.subnet_ids[0]]
    security_group_ids = [var.security_group_id]
  }

  tags = merge(var.tags, {
    Name = var.domain_name
  })
}

# ------------------------------------------------------------------------------
# Access Policy
# ------------------------------------------------------------------------------

resource "aws_opensearch_domain_policy" "main" {
  domain_name = aws_opensearch_domain.main.domain_name

  access_policies = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect    = "Allow"
        Principal = { AWS = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:root" }
        Action    = "es:*"
        Resource  = "${aws_opensearch_domain.main.arn}/*"
      }
    ]
  })
}
