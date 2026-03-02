variable "role_name" {
  type = string
}

variable "domain_name" {
  type = string
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

data "aws_opensearch_domain" "main" {
  domain_name = var.domain_name
}

data "aws_iam_policy_document" "opensearch" {
  statement {
    effect  = "Allow"
    actions = ["es:ESHttp*"]
    resources = [
      "arn:aws:es:${data.aws_region.current.name}:${data.aws_caller_identity.current.account_id}:domain/${var.domain_name}/*"
    ]
  }
}

resource "aws_iam_role_policy" "opensearch" {
  name   = "opensearch-access"
  role   = var.role_name
  policy = data.aws_iam_policy_document.opensearch.json
}
