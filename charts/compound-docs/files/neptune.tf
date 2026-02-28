variable "name_prefix" {
  type    = string
  default = "compound-docs"
}

variable "engine_version" {
  type    = string
  default = "1.2.0.1"
}

variable "instance_class" {
  type    = string
  default = "db.t4g.medium"
}

variable "vpc_id" {
  type = string
}

variable "private_subnet_ids" {
  type = string
}

variable "security_group_id" {
  type = string
}

variable "create_service_linked_role" {
  type    = bool
  default = true
}

variable "write_to_secrets_manager" {
  type    = bool
  default = false
}

variable "secrets_manager_prefix" {
  type    = string
  default = ""
}

resource "aws_iam_service_linked_role" "neptune" {
  count            = var.create_service_linked_role ? 1 : 0
  aws_service_name = "rds.amazonaws.com"
}

resource "aws_neptune_subnet_group" "main" {
  name       = "${var.name_prefix}-neptune"
  subnet_ids = split(",", var.private_subnet_ids)

  tags = {
    Name = "${var.name_prefix}-neptune-subnet-group"
  }
}

resource "aws_neptune_cluster_parameter_group" "main" {
  family = "neptune1.2"
  name   = "${var.name_prefix}-neptune-params"

  parameter {
    name  = "neptune_enable_audit_log"
    value = "1"
  }

  tags = {
    Name = "${var.name_prefix}-neptune-params"
  }
}

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

  tags = {
    Name = "${var.name_prefix}-neptune"
  }

  depends_on = [aws_iam_service_linked_role.neptune]
}

resource "aws_neptune_cluster_instance" "main" {
  identifier         = "${var.name_prefix}-neptune-0"
  cluster_identifier = aws_neptune_cluster.main.id
  instance_class     = var.instance_class
  engine             = "neptune"

  tags = {
    Name = "${var.name_prefix}-neptune-instance"
  }
}

resource "aws_secretsmanager_secret" "neptune" {
  count                   = var.write_to_secrets_manager ? 1 : 0
  name                    = "${var.secrets_manager_prefix}/neptune"
  recovery_window_in_days = 0

  tags = {
    Name = "${var.name_prefix}-neptune-secret"
  }
}

resource "aws_secretsmanager_secret_version" "neptune" {
  count     = var.write_to_secrets_manager ? 1 : 0
  secret_id = aws_secretsmanager_secret.neptune[0].id
  secret_string = jsonencode({
    endpoint        = aws_neptune_cluster.main.endpoint
    reader_endpoint = aws_neptune_cluster.main.reader_endpoint
    port            = tostring(aws_neptune_cluster.main.port)
  })
}

output "endpoint" {
  value = aws_neptune_cluster.main.endpoint
}

output "reader_endpoint" {
  value = aws_neptune_cluster.main.reader_endpoint
}

output "port" {
  value = aws_neptune_cluster.main.port
}
