################################################################################
# Remote State — Phase 1 (Network)
################################################################################

data "terraform_remote_state" "network" {
  backend = "local"
  config = {
    path = "${path.module}/../01-network/terraform.tfstate"
  }
}
