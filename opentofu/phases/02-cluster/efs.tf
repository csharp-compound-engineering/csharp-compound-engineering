################################################################################
# EFS — Shared Storage for Git Repositories
################################################################################

resource "aws_security_group" "efs" {
  count = var.efs_enabled ? 1 : 0

  name        = "${var.cluster_name}-efs"
  description = "Allow NFS access from EKS nodes to EFS"
  vpc_id      = data.terraform_remote_state.network.outputs.vpc_id

  ingress {
    description     = "NFS from EKS nodes"
    from_port       = 2049
    to_port         = 2049
    protocol        = "tcp"
    security_groups = [module.eks.node_security_group_id]
  }

  ingress {
    description     = "NFS from EKS cluster primary"
    from_port       = 2049
    to_port         = 2049
    protocol        = "tcp"
    security_groups = [module.eks.cluster_primary_security_group_id]
  }

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-efs"
  })
}

resource "aws_efs_file_system" "git_repos" {
  count = var.efs_enabled ? 1 : 0

  encrypted        = true
  performance_mode = "generalPurpose"
  throughput_mode  = "bursting"

  lifecycle_policy {
    transition_to_ia = "AFTER_30_DAYS"
  }

  tags = merge(local.common_tags, {
    Name = "${var.cluster_name}-git-repos"
  })
}

resource "aws_efs_mount_target" "git_repos" {
  count = var.efs_enabled ? length(data.terraform_remote_state.network.outputs.private_subnets) : 0

  file_system_id  = aws_efs_file_system.git_repos[0].id
  subnet_id       = data.terraform_remote_state.network.outputs.private_subnets[count.index]
  security_groups = [aws_security_group.efs[0].id]
}

resource "aws_efs_access_point" "git_repos" {
  count = var.efs_enabled ? 1 : 0

  file_system_id = aws_efs_file_system.git_repos[0].id

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
    Name = "${var.cluster_name}-git-repos"
  })
}
