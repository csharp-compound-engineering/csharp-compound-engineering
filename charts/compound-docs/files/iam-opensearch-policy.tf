variable "role_name" {
  type = string
}

variable "collection_name" {
  type = string
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

data "aws_opensearchserverless_collection" "main" {
  name = var.collection_name
}

data "aws_iam_policy_document" "opensearch" {
  statement {
    effect  = "Allow"
    actions = ["aoss:APIAccessAll"]
    resources = [
      "arn:aws:aoss:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:collection/${data.aws_opensearchserverless_collection.main.id}"
    ]
  }
}

resource "aws_iam_role_policy" "opensearch" {
  name   = "opensearch-access"
  role   = var.role_name
  policy = data.aws_iam_policy_document.opensearch.json
}
