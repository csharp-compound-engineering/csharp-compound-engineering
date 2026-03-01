################################################################################
# Outputs — Neptune Module
################################################################################

output "endpoint" {
  description = "Writer endpoint for the Neptune cluster"
  value       = aws_neptune_cluster.main.endpoint
}

output "reader_endpoint" {
  description = "Reader endpoint for the Neptune cluster"
  value       = aws_neptune_cluster.main.reader_endpoint
}

output "port" {
  description = "Port number for the Neptune cluster"
  value       = aws_neptune_cluster.main.port
}

output "cluster_resource_id" {
  description = "Neptune cluster resource ID (used for IAM authentication)"
  value       = aws_neptune_cluster.main.cluster_resource_id
}

output "cluster_identifier" {
  description = "Neptune cluster identifier"
  value       = aws_neptune_cluster.main.cluster_identifier
}
