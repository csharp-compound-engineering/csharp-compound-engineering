################################################################################
# Data Sources — Phase 0: Prerequisites
################################################################################

data "aws_caller_identity" "current" {}

data "terraform_remote_state" "network" {
  backend = "local"
  config = {
    path = "${path.module}/../01-network/terraform.tfstate"
  }
}
