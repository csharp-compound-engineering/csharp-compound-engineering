################################################################################
# Neptune Module — Provisioned Cluster
################################################################################

# ------------------------------------------------------------------------------
# Service-Linked Role (one per account)
# ------------------------------------------------------------------------------

resource "aws_iam_service_linked_role" "neptune" {
  count            = var.create_service_linked_role ? 1 : 0
  aws_service_name = "rds.amazonaws.com"
}

# ------------------------------------------------------------------------------
# Subnet Group
# ------------------------------------------------------------------------------

resource "aws_neptune_subnet_group" "main" {
  name       = "${var.name_prefix}-neptune"
  subnet_ids = var.subnet_ids

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-neptune-subnet-group"
  })
}

# ------------------------------------------------------------------------------
# Cluster Parameter Group
# ------------------------------------------------------------------------------

resource "aws_neptune_cluster_parameter_group" "main" {
  family = "neptune1.2"
  name   = "${var.name_prefix}-neptune-params"

  parameter {
    name  = "neptune_enable_audit_log"
    value = "1"
  }

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-neptune-params"
  })
}

# ------------------------------------------------------------------------------
# Neptune Cluster
# ------------------------------------------------------------------------------

resource "aws_neptune_cluster" "main" {
  cluster_identifier                   = "${var.name_prefix}-neptune"
  engine                               = "neptune"
  engine_version                       = var.engine_version
  neptune_subnet_group_name            = aws_neptune_subnet_group.main.name
  neptune_cluster_parameter_group_name = aws_neptune_cluster_parameter_group.main.name
  vpc_security_group_ids               = [var.security_group_id]
  storage_encrypted                    = true
  iam_database_authentication_enabled  = true
  skip_final_snapshot                  = true

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-neptune"
  })

  depends_on = [aws_iam_service_linked_role.neptune]
}

# ------------------------------------------------------------------------------
# Neptune Instance
# ------------------------------------------------------------------------------

resource "aws_neptune_cluster_instance" "main" {
  identifier         = "${var.name_prefix}-neptune-0"
  cluster_identifier = aws_neptune_cluster.main.id
  instance_class     = var.instance_class
  engine             = "neptune"

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-neptune-instance"
  })
}
