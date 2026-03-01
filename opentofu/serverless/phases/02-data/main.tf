################################################################################
# Phase 2: Data — Neptune + OpenSearch + EFS (Serverless)
################################################################################

locals {
  common_tags = {
    OpenTofu    = "true"
    Environment = var.environment
    Stack       = var.stack_name
  }
}

################################################################################
# Neptune
################################################################################

module "neptune" {
  source = "../../../modules/neptune"

  name_prefix              = var.stack_name
  vpc_id                   = data.terraform_remote_state.network.outputs.vpc_id
  subnet_ids               = data.terraform_remote_state.network.outputs.private_subnets
  security_group_id        = data.terraform_remote_state.network.outputs.neptune_security_group_id
  create_service_linked_role = false

  tags = local.common_tags
}

################################################################################
# OpenSearch
################################################################################

module "opensearch" {
  source = "../../../modules/opensearch"

  domain_name       = "${var.stack_name}-vectors"
  vpc_id            = data.terraform_remote_state.network.outputs.vpc_id
  subnet_ids        = data.terraform_remote_state.network.outputs.private_subnets
  security_group_id = data.terraform_remote_state.network.outputs.opensearch_security_group_id

  tags = local.common_tags
}

################################################################################
# EFS — Shared Storage for Git Repositories
################################################################################

resource "aws_efs_file_system" "git_repos" {
  encrypted        = true
  performance_mode = "generalPurpose"
  throughput_mode  = "bursting"

  lifecycle_policy {
    transition_to_ia = "AFTER_30_DAYS"
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-git-repos"
  })
}

resource "aws_efs_mount_target" "git_repos" {
  count = length(data.terraform_remote_state.network.outputs.private_subnets)

  file_system_id  = aws_efs_file_system.git_repos.id
  subnet_id       = data.terraform_remote_state.network.outputs.private_subnets[count.index]
  security_groups = [data.terraform_remote_state.network.outputs.efs_security_group_id]
}

resource "aws_efs_access_point" "git_repos" {
  file_system_id = aws_efs_file_system.git_repos.id

  posix_user {
    uid = 1000
    gid = 1000
  }

  root_directory {
    path = "/git-repos"

    creation_info {
      owner_uid   = 1000
      owner_gid   = 1000
      permissions = "755"
    }
  }

  tags = merge(local.common_tags, {
    Name = "${var.stack_name}-git-repos"
  })
}

################################################################################
# Secrets Manager — Write Neptune + OpenSearch endpoints
################################################################################

resource "aws_secretsmanager_secret_version" "neptune" {
  secret_id = data.terraform_remote_state.prereqs.outputs.neptune_secret_id
  secret_string = jsonencode({
    endpoint        = module.neptune.endpoint
    reader_endpoint = module.neptune.reader_endpoint
    port            = tostring(module.neptune.port)
  })
}

resource "aws_secretsmanager_secret_version" "opensearch" {
  secret_id = data.terraform_remote_state.prereqs.outputs.opensearch_secret_id
  secret_string = jsonencode({
    endpoint = module.opensearch.domain_endpoint
  })
}
