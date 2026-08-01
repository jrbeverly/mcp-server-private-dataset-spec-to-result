# ==============================================================================
# Corpus Deployment — Outputs
# ==============================================================================

output "s3_bucket_name" {
  description = "S3 bucket for raw and processed artifacts"
  value       = aws_s3_bucket.artifacts.id
}

output "s3_bucket_arn" {
  description = "S3 bucket ARN"
  value       = aws_s3_bucket.artifacts.arn
}

output "rds_endpoint" {
  description = "RDS PostgreSQL endpoint (empty if using external Postgres)"
  value       = var.create_rds ? aws_db_instance.postgres[0].endpoint : ""
}

output "rds_port" {
  description = "RDS PostgreSQL port"
  value       = var.create_rds ? aws_db_instance.postgres[0].port : ""
}

output "alb_dns_name" {
  description = "ALB public DNS name for HTTPS access"
  value       = aws_lb.service.dns_name
}

output "service_url" {
  description = "Full HTTPS URL for the MCP endpoint"
  value       = "https://${aws_lb.service.dns_name}"
}

output "mcp_endpoint_url" {
  description = "MCP endpoint URL to configure in ChatGPT"
  value       = "https://${aws_lb.service.dns_name}/mcp"
}

output "ecs_cluster_name" {
  description = "ECS cluster name"
  value       = aws_ecs_cluster.service.name
}

output "ecs_service_name" {
  description = "ECS service name"
  value       = aws_ecs_service.service.name
}

output "cloudwatch_log_group" {
  description = "CloudWatch log group for service logs"
  value       = aws_cloudwatch_log_group.service.name
}

output "ssm_auth_token_path" {
  description = "SSM Parameter Store path for the auth token"
  value       = aws_ssm_parameter.auth_token.name
}

output "ssm_postgres_connection_string_path" {
  description = "SSM Parameter Store path for the PostgreSQL connection string"
  value       = aws_ssm_parameter.postgres_connection_string.name
}
