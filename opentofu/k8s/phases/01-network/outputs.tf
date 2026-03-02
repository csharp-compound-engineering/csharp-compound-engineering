################################################################################
# Outputs — Phase 1: Network
################################################################################

# ------------------------------------------------------------------------------
# VPC
# ------------------------------------------------------------------------------

output "vpc_id" {
  description = "ID of the VPC"
  value       = module.vpc.vpc_id
}

output "private_subnets" {
  description = "IDs of the private subnets (EKS worker nodes run here)"
  value       = module.vpc.private_subnets
}

output "public_subnets" {
  description = "IDs of the public subnets (internet-facing load balancers)"
  value       = module.vpc.public_subnets
}

# ------------------------------------------------------------------------------
# DNS
# ------------------------------------------------------------------------------

output "internal_dns_zone_id" {
  description = "ID of the Route 53 private hosted zone"
  value       = aws_route53_zone.internal.zone_id
}

output "internal_dns_zone_arn" {
  description = "ARN of the Route 53 private hosted zone"
  value       = aws_route53_zone.internal.arn
}

output "internal_dns_zone_name" {
  description = "Name of the Route 53 private hosted zone"
  value       = aws_route53_zone.internal.name
}

# ------------------------------------------------------------------------------
# ExternalDNS
# ------------------------------------------------------------------------------

output "external_dns_role_arn" {
  description = "IAM role ARN for ExternalDNS (Pod Identity)"
  value       = var.external_dns_enabled ? aws_iam_role.external_dns[0].arn : null
}

# ------------------------------------------------------------------------------
# VPN
# ------------------------------------------------------------------------------

output "vpn_endpoint_id" {
  description = "ID of the Client VPN endpoint"
  value       = var.vpn_enabled ? aws_ec2_client_vpn_endpoint.this[0].id : null
}

output "vpn_client_config_file" {
  description = "Path to the generated .ovpn client configuration file"
  value       = var.vpn_enabled ? local_file.vpn_client_config[0].filename : null
}

output "vpn_dns_name" {
  description = "DNS name of the Client VPN endpoint"
  value       = var.vpn_enabled ? aws_ec2_client_vpn_endpoint.this[0].dns_name : null
}
