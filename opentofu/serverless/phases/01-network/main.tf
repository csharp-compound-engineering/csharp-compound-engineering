################################################################################
# Phase 1: Network — VPC + Security Groups + VPC Endpoints (Serverless)
################################################################################

data "aws_availability_zones" "available" {
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

locals {
  azs = slice(data.aws_availability_zones.available.names, 0, 3)

  private_subnets = [cidrsubnet(var.vpc_cidr, 8, 1), cidrsubnet(var.vpc_cidr, 8, 2), cidrsubnet(var.vpc_cidr, 8, 3)]
  public_subnets  = [cidrsubnet(var.vpc_cidr, 8, 101), cidrsubnet(var.vpc_cidr, 8, 102), cidrsubnet(var.vpc_cidr, 8, 103)]

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

  # No NAT gateway — Lambda uses VPC endpoints; Fargate uses public subnets
  enable_nat_gateway = false

  # DNS — required for VPC endpoints
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
  name        = "${var.stack_name}-lambda"
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
}

resource "aws_security_group" "fargate" {
  name        = "${var.stack_name}-fargate"
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
}

resource "aws_security_group" "neptune" {
  name        = "${var.stack_name}-neptune"
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
}

resource "aws_security_group" "opensearch" {
  name        = "${var.stack_name}-opensearch"
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
}

resource "aws_security_group" "efs" {
  name        = "${var.stack_name}-efs"
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
}

################################################################################
# VPC Endpoints — Shared Security Group
################################################################################

resource "aws_security_group" "vpc_endpoints" {
  name        = "${var.stack_name}-vpc-endpoints"
  description = "Security group for VPC interface endpoints"
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
    Name = "${var.stack_name}-vpc-endpoints"
  })
}

################################################################################
# VPC Interface Endpoints
################################################################################

resource "aws_vpc_endpoint" "bedrock_runtime" {
  vpc_id              = module.vpc.vpc_id
  service_name        = "com.amazonaws.${var.region}.bedrock-runtime"
  vpc_endpoint_type   = "Interface"
  subnet_ids          = module.vpc.private_subnets
  security_group_ids  = [aws_security_group.vpc_endpoints.id]
  private_dns_enabled = true

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-bedrock-runtime"
  })
}

resource "aws_vpc_endpoint" "logs" {
  vpc_id              = module.vpc.vpc_id
  service_name        = "com.amazonaws.${var.region}.logs"
  vpc_endpoint_type   = "Interface"
  subnet_ids          = module.vpc.private_subnets
  security_group_ids  = [aws_security_group.vpc_endpoints.id]
  private_dns_enabled = true

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-logs"
  })
}

resource "aws_vpc_endpoint" "secretsmanager" {
  vpc_id              = module.vpc.vpc_id
  service_name        = "com.amazonaws.${var.region}.secretsmanager"
  vpc_endpoint_type   = "Interface"
  subnet_ids          = module.vpc.private_subnets
  security_group_ids  = [aws_security_group.vpc_endpoints.id]
  private_dns_enabled = true

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-secretsmanager"
  })
}

resource "aws_vpc_endpoint" "sts" {
  vpc_id              = module.vpc.vpc_id
  service_name        = "com.amazonaws.${var.region}.sts"
  vpc_endpoint_type   = "Interface"
  subnet_ids          = module.vpc.private_subnets
  security_group_ids  = [aws_security_group.vpc_endpoints.id]
  private_dns_enabled = true

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-sts"
  })
}

################################################################################
# VPC Gateway Endpoint (free)
################################################################################

resource "aws_vpc_endpoint" "s3" {
  vpc_id            = module.vpc.vpc_id
  service_name      = "com.amazonaws.${var.region}.s3"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = module.vpc.private_route_table_ids

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-s3"
  })
}
