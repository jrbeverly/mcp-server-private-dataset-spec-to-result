# ==============================================================================
# Corpus Deployment — Input Variables
# ==============================================================================

# -- Corpus Identity -----------------------------------------------------------

variable "corpus_id" {
  description = "Corpus identifier (e.g., 'shopify-ecosystem'). One deployment per corpus."
  type        = string
  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9_-]{2,63}$", var.corpus_id))
    error_message = "Corpus ID must be 3-64 lowercase alphanumeric characters, hyphens, or underscores."
  }
}

variable "deployment_name" {
  description = "Human-readable deployment name"
  type        = string
  default     = ""
}

# -- Environment ---------------------------------------------------------------

variable "environment" {
  description = "Deployment environment (staging, production)"
  type        = string
  default     = "staging"
  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "Environment must be 'staging' or 'production'."
  }
}

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

# -- Networking ----------------------------------------------------------------

variable "vpc_id" {
  description = "VPC ID for all resources"
  type        = string
}

variable "alb_subnet_ids" {
  description = "Public subnet IDs for the ALB"
  type        = list(string)
}

variable "service_subnet_ids" {
  description = "Private subnet IDs for the ECS service"
  type        = list(string)
}

variable "rds_subnet_ids" {
  description = "Subnet IDs for RDS (required if create_rds is true)"
  type        = list(string)
  default     = []
}

# -- Compute -------------------------------------------------------------------

variable "service_cpu" {
  description = "ECS task CPU units (256, 512, 1024, 2048, 4096)"
  type        = number
  default     = 512
}

variable "service_memory" {
  description = "ECS task memory in MiB"
  type        = number
  default     = 1024
}

variable "service_desired_count" {
  description = "Number of service instances"
  type        = number
  default     = 1
}

# -- Container Image -----------------------------------------------------------

variable "container_image_repository" {
  description = "Container image registry (ECR repository URL or Docker Hub)"
  type        = string
  default     = ""
}

variable "image_tag" {
  description = "Container image tag to deploy"
  type        = string
  default     = "latest"
}

# -- Database ------------------------------------------------------------------

variable "create_rds" {
  description = "Create an RDS PostgreSQL instance (false = use external Postgres)"
  type        = bool
  default     = false
}

variable "rds_instance_class" {
  description = "RDS instance class (only used if create_rds is true)"
  type        = string
  default     = "db.t4g.micro"
}

variable "rds_password" {
  description = "RDS master password (only used if create_rds is true)"
  type        = string
  sensitive   = true
  default     = ""
}

variable "postgres_connection_string" {
  description = "PostgreSQL connection string (external Postgres if create_rds is false)"
  type        = string
  sensitive   = true
}

# -- Authentication ------------------------------------------------------------

variable "auth_token" {
  description = "Static bearer token for API access"
  type        = string
  sensitive   = true
}

# -- TLS -----------------------------------------------------------------------

variable "certificate_arn" {
  description = "ARN of the ACM certificate for HTTPS"
  type        = string
}

# -- Operations ----------------------------------------------------------------

variable "log_retention_days" {
  description = "CloudWatch log retention in days"
  type        = number
  default     = 30
}

variable "allow_version_pinning" {
  description = "Allow API requests to pin to a specific dataset version"
  type        = bool
  default     = true
}
