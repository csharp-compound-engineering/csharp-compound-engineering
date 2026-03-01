################################################################################
# Phase 1: Network — VPC + DNS + VPN
################################################################################

# ------------------------------------------------------------------------------
# Computed Locals
# ------------------------------------------------------------------------------

data "aws_availability_zones" "available" {
  filter {
    name   = "opt-in-status"
    values = ["opt-in-not-required"]
  }
}

locals {
  azs = slice(data.aws_availability_zones.available.names, 0, 3)

  private_subnets = ["10.0.1.0/24", "10.0.2.0/24", "10.0.3.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24", "10.0.103.0/24"]

  common_tags = {
    OpenTofu    = "true"
    Environment = var.environment
    Cluster     = var.cluster_name
  }
}

# ------------------------------------------------------------------------------
# VPC
# ------------------------------------------------------------------------------

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 6.0"

  name = "${var.cluster_name}-vpc"
  cidr = var.vpc_cidr

  azs             = local.azs
  private_subnets = local.private_subnets
  public_subnets  = local.public_subnets

  # NAT Gateway — required for private-subnet internet access (image pulls, AWS APIs)
  enable_nat_gateway     = true
  single_nat_gateway     = var.single_nat_gateway
  one_nat_gateway_per_az = !var.single_nat_gateway

  # DNS — required for EKS
  enable_dns_hostnames = true
  enable_dns_support   = true

  # Do not auto-assign public IPs in public subnets (LBs get their own)
  map_public_ip_on_launch = false

  # EKS subnet discovery tags — internet-facing load balancers
  public_subnet_tags = {
    "kubernetes.io/role/elb"                    = "1"
    "kubernetes.io/cluster/${var.cluster_name}" = "shared"
  }

  # EKS subnet discovery tags — internal load balancers and pods
  private_subnet_tags = {
    "kubernetes.io/role/internal-elb"           = "1"
    "kubernetes.io/cluster/${var.cluster_name}" = "shared"
  }

  tags = local.common_tags
}

# ------------------------------------------------------------------------------
# Route 53 Private Hosted Zone
# ------------------------------------------------------------------------------

resource "aws_route53_zone" "internal" {
  name = var.internal_dns_zone

  vpc {
    vpc_id = module.vpc.vpc_id
  }

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-internal-zone"
  })

  # On destroy, purge any records created outside OpenTofu (e.g. by ExternalDNS)
  # before attempting to delete the hosted zone.
  provisioner "local-exec" {
    when        = destroy
    interpreter = ["pwsh", "-NoProfile", "-Command"]
    command     = "${path.module}/../../scripts/clear-route53-zone.ps1 -ZoneId ${self.zone_id}"
  }
}

# ------------------------------------------------------------------------------
# AWS Client VPN
# ------------------------------------------------------------------------------

# TLS Certificates (self-signed CA + server + client for mutual TLS)

resource "tls_private_key" "ca" {
  count     = var.vpn_enabled ? 1 : 0
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_self_signed_cert" "ca" {
  count           = var.vpn_enabled ? 1 : 0
  private_key_pem = tls_private_key.ca[0].private_key_pem

  subject {
    common_name  = "${var.cluster_name}-vpn-ca"
    organization = var.cluster_name
  }

  validity_period_hours = 87600 # 10 years
  is_ca_certificate     = true

  allowed_uses = [
    "cert_signing",
    "crl_signing",
  ]
}

# Server certificate
resource "tls_private_key" "server" {
  count     = var.vpn_enabled ? 1 : 0
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_cert_request" "server" {
  count           = var.vpn_enabled ? 1 : 0
  private_key_pem = tls_private_key.server[0].private_key_pem

  subject {
    common_name  = "${var.cluster_name}-vpn-server"
    organization = var.cluster_name
  }

  dns_names = ["${var.cluster_name}-vpn-server"]
}

resource "tls_locally_signed_cert" "server" {
  count              = var.vpn_enabled ? 1 : 0
  cert_request_pem   = tls_cert_request.server[0].cert_request_pem
  ca_private_key_pem = tls_private_key.ca[0].private_key_pem
  ca_cert_pem        = tls_self_signed_cert.ca[0].cert_pem

  validity_period_hours = 87600 # 10 years

  allowed_uses = [
    "digital_signature",
    "key_encipherment",
    "server_auth",
  ]
}

# Client certificate
resource "tls_private_key" "client" {
  count     = var.vpn_enabled ? 1 : 0
  algorithm = "RSA"
  rsa_bits  = 2048
}

resource "tls_cert_request" "client" {
  count           = var.vpn_enabled ? 1 : 0
  private_key_pem = tls_private_key.client[0].private_key_pem

  subject {
    common_name  = "${var.cluster_name}-vpn-client"
    organization = var.cluster_name
  }
}

resource "tls_locally_signed_cert" "client" {
  count              = var.vpn_enabled ? 1 : 0
  cert_request_pem   = tls_cert_request.client[0].cert_request_pem
  ca_private_key_pem = tls_private_key.ca[0].private_key_pem
  ca_cert_pem        = tls_self_signed_cert.ca[0].cert_pem

  validity_period_hours = 87600 # 10 years

  allowed_uses = [
    "digital_signature",
    "key_encipherment",
    "client_auth",
  ]
}

# ACM Certificates

resource "aws_acm_certificate" "vpn_server" {
  count             = var.vpn_enabled ? 1 : 0
  private_key       = tls_private_key.server[0].private_key_pem
  certificate_body  = tls_locally_signed_cert.server[0].cert_pem
  certificate_chain = tls_self_signed_cert.ca[0].cert_pem

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-vpn-server-cert"
  })
}

resource "aws_acm_certificate" "vpn_client" {
  count             = var.vpn_enabled ? 1 : 0
  private_key       = tls_private_key.ca[0].private_key_pem
  certificate_body  = tls_self_signed_cert.ca[0].cert_pem
  certificate_chain = tls_self_signed_cert.ca[0].cert_pem

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-vpn-client-ca-cert"
  })
}

# CloudWatch Logging

resource "aws_cloudwatch_log_group" "vpn" {
  count             = var.vpn_enabled ? 1 : 0
  name              = "/aws/vpn/${var.cluster_name}"
  retention_in_days = 30

  tags = local.common_tags
}

resource "aws_cloudwatch_log_stream" "vpn" {
  count          = var.vpn_enabled ? 1 : 0
  name           = "connections"
  log_group_name = aws_cloudwatch_log_group.vpn[0].name
}

# Security Group

resource "aws_security_group" "vpn" {
  count       = var.vpn_enabled ? 1 : 0
  name        = "${var.cluster_name}-vpn"
  description = "Security group for Client VPN endpoint"
  vpc_id      = module.vpc.vpc_id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = [var.vpc_cidr]
    description = "Allow all traffic to VPC"
  }

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-vpn"
  })
}

# Client VPN Endpoint

resource "aws_ec2_client_vpn_endpoint" "this" {
  count = var.vpn_enabled ? 1 : 0

  description            = "${var.cluster_name} Client VPN"
  server_certificate_arn = aws_acm_certificate.vpn_server[0].arn
  client_cidr_block      = var.vpn_client_cidr
  transport_protocol     = "udp"
  split_tunnel           = true
  dns_servers            = [cidrhost(var.vpc_cidr, 2)]
  security_group_ids     = [aws_security_group.vpn[0].id]
  vpc_id                 = module.vpc.vpc_id

  authentication_options {
    type                       = "certificate-authentication"
    root_certificate_chain_arn = aws_acm_certificate.vpn_client[0].arn
  }

  connection_log_options {
    enabled               = true
    cloudwatch_log_group  = aws_cloudwatch_log_group.vpn[0].name
    cloudwatch_log_stream = aws_cloudwatch_log_stream.vpn[0].name
  }

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-vpn"
  })
}

# Network Association (single private subnet — ~$73/month per association)

resource "aws_ec2_client_vpn_network_association" "this" {
  count = var.vpn_enabled ? 1 : 0

  client_vpn_endpoint_id = aws_ec2_client_vpn_endpoint.this[0].id
  subnet_id              = module.vpc.private_subnets[0]
}

# Authorization Rule — allow VPN clients to access the entire VPC

resource "aws_ec2_client_vpn_authorization_rule" "vpc" {
  count = var.vpn_enabled ? 1 : 0

  client_vpn_endpoint_id = aws_ec2_client_vpn_endpoint.this[0].id
  target_network_cidr    = var.vpc_cidr
  authorize_all_groups   = true
  description            = "Allow all VPN clients to access VPC"
}

# Generated .ovpn Client Configuration

resource "local_file" "vpn_client_config" {
  count = var.vpn_enabled ? 1 : 0

  filename        = "${path.module}/generated/client.ovpn"
  file_permission = "0600"

  content = <<-EOT
    client
    dev tun
    proto udp
    remote ${replace(aws_ec2_client_vpn_endpoint.this[0].dns_name, "*.", "")} 443
    remote-random-hostname
    resolv-retry infinite
    nobind
    remote-cert-tls server
    cipher AES-256-GCM
    verb 3

    <ca>
    ${tls_self_signed_cert.ca[0].cert_pem}
    </ca>

    <cert>
    ${tls_locally_signed_cert.client[0].cert_pem}
    </cert>

    <key>
    ${tls_private_key.client[0].private_key_pem}
    </key>

    reneg-sec 0
  EOT
}
