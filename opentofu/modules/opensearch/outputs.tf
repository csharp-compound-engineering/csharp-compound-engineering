################################################################################
# Outputs — OpenSearch Module
################################################################################

output "domain_endpoint" {
  description = "HTTPS endpoint for the OpenSearch domain"
  value       = "https://${aws_opensearch_domain.main.endpoint}"
}

output "domain_arn" {
  description = "ARN of the OpenSearch domain"
  value       = aws_opensearch_domain.main.arn
}

output "domain_id" {
  description = "Unique identifier for the OpenSearch domain"
  value       = aws_opensearch_domain.main.domain_id
}

output "domain_name" {
  description = "Name of the OpenSearch domain"
  value       = aws_opensearch_domain.main.domain_name
}
