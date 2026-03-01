################################################################################
# Remote State — Phase 00 + 01
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
