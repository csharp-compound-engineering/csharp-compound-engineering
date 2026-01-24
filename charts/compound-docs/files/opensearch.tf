variable "name_prefix" {
  type    = string
  default = "compound-docs"
}

variable "collection_name" {
  type    = string
  default = "compound-docs-vectors"
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

variable "write_to_secrets_manager" {
  type    = bool
  default = false
}

variable "secrets_manager_prefix" {
  type    = string
  default = ""
}

data "aws_caller_identity" "current" {}

resource "aws_opensearchserverless_security_policy" "encryption" {
  name = "${var.name_prefix}-encryption"
  type = "encryption"
  policy = jsonencode({
    Rules = [
      {
        ResourceType = "collection"
        Resource     = ["collection/${var.collection_name}"]
      }
    ]
    AWSOwnedKey = true
  })
}

resource "aws_opensearchserverless_security_policy" "network" {
  name = "${var.name_prefix}-network"
  type = "network"
  policy = jsonencode([
    {
      Rules = [
        {
          ResourceType = "collection"
          Resource     = ["collection/${var.collection_name}"]
        }
      ]
      AllowFromPublic = false
      SourceVPCEs     = []
    }
  ])
}

resource "aws_opensearchserverless_access_policy" "data" {
  name = "${var.name_prefix}-data"
  type = "data"
  policy = jsonencode([
    {
      Rules = [
        {
          ResourceType = "index"
          Resource     = ["index/${var.collection_name}/*"]
          Permission   = ["aoss:CreateIndex", "aoss:ReadDocument", "aoss:WriteDocument", "aoss:UpdateIndex", "aoss:DescribeIndex"]
        },
        {
          ResourceType = "collection"
          Resource     = ["collection/${var.collection_name}"]
          Permission   = ["aoss:CreateCollectionItems", "aoss:DescribeCollectionItems", "aoss:UpdateCollectionItems"]
        }
      ]
      Principal = ["arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"]
    }
  ])
}

resource "aws_opensearchserverless_collection" "main" {
  name = var.collection_name
  type = "VECTORSEARCH"

  tags = {
    Name = var.collection_name
  }

  depends_on = [
    aws_opensearchserverless_security_policy.encryption,
    aws_opensearchserverless_security_policy.network,
    aws_opensearchserverless_access_policy.data,
  ]
}

resource "aws_secretsmanager_secret" "opensearch" {
  count = var.write_to_secrets_manager ? 1 : 0
  name  = "${var.secrets_manager_prefix}/opensearch"

  tags = {
    Name = "${var.name_prefix}-opensearch-secret"
  }
}

resource "aws_secretsmanager_secret_version" "opensearch" {
  count     = var.write_to_secrets_manager ? 1 : 0
  secret_id = aws_secretsmanager_secret.opensearch[0].id
  secret_string = jsonencode({
    endpoint      = aws_opensearchserverless_collection.main.collection_endpoint
    collection_id = aws_opensearchserverless_collection.main.id
  })
}

output "endpoint" {
  value = aws_opensearchserverless_collection.main.collection_endpoint
}

output "collection_id" {
  value = aws_opensearchserverless_collection.main.id
}
