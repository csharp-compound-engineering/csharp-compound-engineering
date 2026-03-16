################################################################################
# Phase 1: Network — VPC + Security Groups + NAT Gateway (Serverless)
################################################################################

data "aws_availability_zones" "available" {
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

locals {
  azs = slice(data.aws_availability_zones.available.names, 0, 2)

  private_subnets = [cidrsubnet(var.vpc_cidr, 8, 1), cidrsubnet(var.vpc_cidr, 8, 2)]
  public_subnets  = [cidrsubnet(var.vpc_cidr, 8, 101), cidrsubnet(var.vpc_cidr, 8, 102)]

  common_tags = {
    OpenTofu    = "true"
    Environment = var.environment
    Stack       = var.stack_name
  }
}

################################################################################
# VPC
################################################################################

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 6.0"

  name = "${var.stack_name}-vpc"
  cidr = var.vpc_cidr

  azs             = local.azs
  private_subnets = local.private_subnets
  public_subnets  = local.public_subnets

  # Single NAT gateway — Lambda and Fargate use NAT for AWS API access
  enable_nat_gateway = true
  single_nat_gateway = true

  # DNS — required for private subnet AWS API access via NAT
  enable_dns_hostnames = true
  enable_dns_support   = true

  # Auto-assign public IPs in public subnets (for Fargate with assign_public_ip)
  map_public_ip_on_launch = false

  tags = local.common_tags
}

################################################################################
# Security Groups
################################################################################

resource "aws_security_group" "lambda" {
  name_prefix = "${var.stack_name}-lambda"
  description = "Security group for Lambda functions"
  vpc_id      = module.vpc.vpc_id

  egress {
    description = "Allow all outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-lambda"
  })

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "fargate" {
  name_prefix = "${var.stack_name}-fargate"
  description = "Security group for Fargate tasks"
  vpc_id      = module.vpc.vpc_id

  egress {
    description = "Allow all outbound"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-fargate"
  })

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "neptune" {
  name_prefix = "${var.stack_name}-neptune"
  description = "Security group for Neptune cluster"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description     = "Neptune from Lambda"
    from_port       = 8182
    to_port         = 8182
    protocol        = "tcp"
    security_groups = [aws_security_group.lambda.id]
  }

  ingress {
    description     = "Neptune from Fargate"
    from_port       = 8182
    to_port         = 8182
    protocol        = "tcp"
    security_groups = [aws_security_group.fargate.id]
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-neptune"
  })

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "opensearch" {
  name_prefix = "${var.stack_name}-opensearch"
  description = "Security group for OpenSearch domain"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description     = "HTTPS from Lambda"
    from_port       = 443
    to_port         = 443
    protocol        = "tcp"
    security_groups = [aws_security_group.lambda.id]
  }

  ingress {
    description     = "HTTPS from Fargate"
    from_port       = 443
    to_port         = 443
    protocol        = "tcp"
    security_groups = [aws_security_group.fargate.id]
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-opensearch"
  })

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "efs" {
  name_prefix = "${var.stack_name}-efs"
  description = "Security group for EFS"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description     = "NFS from Fargate"
    from_port       = 2049
    to_port         = 2049
    protocol        = "tcp"
    security_groups = [aws_security_group.fargate.id]
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-efs"
  })

  lifecycle {
    create_before_destroy = true
  }
}
