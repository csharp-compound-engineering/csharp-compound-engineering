################################################################################
# Remote State — Phase 00 + 01 + 02
################################################################################

data "terraform_remote_state" "prereqs" {
  backend = "local"
  config = {
    path = "${path.module}/../00-prereqs/terraform.tfstate"
  }
}

data "terraform_remote_state" "network" {
  backend = "local"
  config = {
    path = "${path.module}/../01-network/terraform.tfstate"
  }
}

data "terraform_remote_state" "data" {
  backend = "local"
  config = {
    path = "${path.module}/../02-data/terraform.tfstate"
  }
}

data "aws_secretsmanager_secret_version" "api_keys" {
  secret_id = data.terraform_remote_state.prereqs.outputs.api_keys_secret_id
}
