################################################################################
# Remote State — Phase 0 (Prereqs) + Phase 1 (Network) + Phase 2 (Cluster)
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

data "terraform_remote_state" "cluster" {
  backend = "local"
  config = {
    path = "${path.module}/../02-cluster/terraform.tfstate"
  }
}
