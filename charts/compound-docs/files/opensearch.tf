variable "name_prefix" {
  type    = string
  default = "compound-docs"
}

variable "domain_name" {
  type    = string
  default = "compound-docs-vectors"
}

variable "instance_type" {
  type    = string
  default = "t3.small.search"
}

variable "engine_version" {
  type    = string
  default = "OpenSearch_2.17"
}

variable "ebs_volume_size" {
  type    = number
  default = 20
}

variable "vpc_id" {
  type = string
}

variable "private_subnet_ids" {
  type = string
}

variable "write_to_secrets_manager" {
  type    = bool
  default = false
}

variable "secrets_manager_prefix" {
  type    = string
  default = ""
}

data "aws_caller_identity" "current" {}

data "aws_vpc" "main" {
  id = var.vpc_id
}

resource "aws_security_group" "opensearch" {
  name   = "${var.name_prefix}-opensearch"
  vpc_id = var.vpc_id

  ingress {
    protocol    = "tcp"
    from_port   = 443
    to_port     = 443
    cidr_blocks = [data.aws_vpc.main.cidr_block]
  }

  egress {
    protocol    = "-1"
    from_port   = 0
    to_port     = 0
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name = "${var.name_prefix}-opensearch-sg"
  }
}

resource "aws_opensearch_domain" "main" {
  domain_name    = var.domain_name
  engine_version = var.engine_version

  cluster_config {
    instance_type  = var.instance_type
    instance_count = 1
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

    master_user_options {
      master_user_arn = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${var.name_prefix}-pod-identity"
    }
  }

  vpc_options {
    subnet_ids         = [split(",", var.private_subnet_ids)[0]]
    security_group_ids = [aws_security_group.opensearch.id]
  }

  tags = {
    Name = var.domain_name
  }
}

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

resource "aws_secretsmanager_secret" "opensearch" {
  count                   = var.write_to_secrets_manager ? 1 : 0
  name                    = "${var.secrets_manager_prefix}/opensearch"
  recovery_window_in_days = 0

  tags = {
    Name = "${var.name_prefix}-opensearch-secret"
  }
}

resource "aws_secretsmanager_secret_version" "opensearch" {
  count     = var.write_to_secrets_manager ? 1 : 0
  secret_id = aws_secretsmanager_secret.opensearch[0].id
  secret_string = jsonencode({
    endpoint = aws_opensearch_domain.main.endpoint
  })
}

output "endpoint" {
  value = aws_opensearch_domain.main.endpoint
}
