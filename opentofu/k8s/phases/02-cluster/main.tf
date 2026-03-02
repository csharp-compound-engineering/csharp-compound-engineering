################################################################################
# Phase 2: Cluster — EKS
################################################################################

locals {
  common_tags = {
    OpenTofu    = "true"
    Environment = var.environment
    Cluster     = var.cluster_name
  }
}

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 21.0"

  name               = var.cluster_name
  kubernetes_version = var.cluster_version

  # Networking — from Phase 1 remote state
  vpc_id                   = data.terraform_remote_state.network.outputs.vpc_id
  subnet_ids               = data.terraform_remote_state.network.outputs.private_subnets
  control_plane_subnet_ids = data.terraform_remote_state.network.outputs.private_subnets

  # Endpoint access
  endpoint_public_access  = var.cluster_endpoint_public_access
  endpoint_private_access = var.cluster_endpoint_private_access

  # Access management — API-mode authentication
  authentication_mode                      = "API"
  enable_cluster_creator_admin_permissions = true

  # Add-ons
  addons = {
    coredns = {
      most_recent = true
    }
    kube-proxy = {
      most_recent = true
    }
    vpc-cni = {
      most_recent    = true
      before_compute = true
    }
    eks-pod-identity-agent = {
      most_recent    = true
      before_compute = true
    }
    aws-efs-csi-driver = var.efs_enabled ? {
      most_recent = true
    } : null
  }

  # --------------------------------------------------------------------------
  # Managed Node Groups
  # --------------------------------------------------------------------------
  eks_managed_node_groups = {
    # Small on-demand group for cluster-critical workloads (CoreDNS, kube-proxy)
    system = {
      ami_type       = "AL2023_x86_64_STANDARD"
      instance_types = ["t3.medium"]
      capacity_type  = "ON_DEMAND"

      min_size     = 1
      max_size     = 3
      desired_size = 2

      labels = {
        role = "system"
      }
    }

    # Scalable group for application workloads
    application = {
      ami_type       = "AL2023_x86_64_STANDARD"
      instance_types = var.node_instance_types
      capacity_type  = "ON_DEMAND"

      min_size     = var.node_min_size
      max_size     = var.node_max_size
      desired_size = var.node_desired_size

      labels = {
        role = "application"
      }

      update_config = {
        max_unavailable_percentage = 33
      }
    }
  }

  tags = local.common_tags
}

################################################################################
# VPN → EKS API Server Access
#
# The cluster primary security group only allows traffic from itself and node
# SGs. VPN clients (172.16.0.0/16) need an explicit ingress rule to reach the
# private API endpoint on port 443.
################################################################################

resource "aws_vpc_security_group_ingress_rule" "vpn_to_eks_api" {
  count = var.vpn_enabled ? 1 : 0

  security_group_id            = module.eks.cluster_primary_security_group_id
  referenced_security_group_id = data.terraform_remote_state.network.outputs.vpn_security_group_id
  from_port                    = 443
  to_port                      = 443
  ip_protocol                  = "tcp"
  description                  = "Allow VPN clients to reach EKS API server"

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-vpn-to-eks-api"
  })
}

################################################################################
# Pod Identity Associations
#
# These must live in the cluster phase (not prereqs) because they require the
# EKS cluster to exist. Using module.eks.cluster_name creates an implicit
# dependency so OpenTofu creates the cluster before the associations.
# IAM roles are read from prereqs via remote state.
################################################################################

resource "aws_eks_pod_identity_association" "external_secrets" {
  count           = var.external_secrets_enabled ? 1 : 0
  cluster_name    = module.eks.cluster_name
  namespace       = "external-secrets"
  service_account = "external-secrets"
  role_arn        = data.terraform_remote_state.prereqs.outputs.external_secrets_role_arn
}

resource "aws_eks_pod_identity_association" "external_dns" {
  count           = var.external_dns_enabled ? 1 : 0
  cluster_name    = module.eks.cluster_name
  namespace       = "external-dns"
  service_account = "external-dns"
  role_arn        = data.terraform_remote_state.network.outputs.external_dns_role_arn
}

resource "aws_eks_pod_identity_association" "crossplane_provider_aws" {
  count           = var.crossplane_enabled ? 1 : 0
  cluster_name    = module.eks.cluster_name
  namespace       = "crossplane-system"
  service_account = "provider-opentofu"
  role_arn        = data.terraform_remote_state.prereqs.outputs.crossplane_provider_aws_role_arn
}

resource "aws_eks_pod_identity_association" "efs_csi_driver" {
  count           = var.efs_enabled ? 1 : 0
  cluster_name    = module.eks.cluster_name
  namespace       = "kube-system"
  service_account = "efs-csi-controller-sa"
  role_arn        = data.terraform_remote_state.prereqs.outputs.efs_csi_driver_role_arn
}
