# ==============================================================================
# Corpus Deployment — Main Infrastructure
# ==============================================================================
# One deployment = one intelligence corpus.
# A second corpus means a second deployment with the same system shape,
# not a new tenant inside this deployment.
# ==============================================================================

terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }

  # Backend should be configured per-deployment via partial configuration.
  # Example:
  #   terraform {
  #     backend "s3" {
  #       bucket = "corpus-tfstate"
  #       key    = "corpus-deployment/${var.corpus_id}/${var.environment}.tfstate"
  #       region = var.aws_region
  #     }
  #   }
}

provider "aws" {
  region = var.aws_region
}

# ==============================================================================
# Artifact Storage (S3)
# ==============================================================================

resource "aws_s3_bucket" "artifacts" {
  bucket = "corpus-${var.corpus_id}-${var.environment}-artifacts"
}

resource "aws_s3_bucket_versioning" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_lifecycle_configuration" "artifacts" {
  bucket = aws_s3_bucket.artifacts.id

  # Retain noncurrent versions for 90 days, then expire
  rule {
    id     = "expire-old-versions"
    status = "Enabled"

    noncurrent_version_expiration {
      noncurrent_days = 90
    }
  }
}

# ==============================================================================
# RDS PostgreSQL (optional — use external Postgres for zero-cost-when-idle)
# ==============================================================================

resource "aws_db_subnet_group" "postgres" {
  count = var.create_rds ? 1 : 0

  name       = "corpus-${var.corpus_id}-${var.environment}"
  subnet_ids = var.rds_subnet_ids

  tags = {
    CorpusId    = var.corpus_id
    Environment = var.environment
  }
}

resource "aws_db_instance" "postgres" {
  count = var.create_rds ? 1 : 0

  identifier = "corpus-${var.corpus_id}-${var.environment}"

  engine         = "postgres"
  engine_version = "16"
  instance_class = var.rds_instance_class

  allocated_storage     = 20
  max_allocated_storage = 100
  storage_encrypted     = true

  db_name  = "corpus"
  username = "corpus_admin"
  password = var.rds_password

  db_subnet_group_name   = aws_db_subnet_group.postgres[0].name
  vpc_security_group_ids = [aws_security_group.rds[0].id]

  backup_retention_period = 7
  backup_window           = "03:00-04:00"
  maintenance_window      = "sun:04:00-sun:05:00"

  skip_final_snapshot = var.environment != "production"
  deletion_protection = var.environment == "production"

  tags = {
    CorpusId    = var.corpus_id
    Environment = var.environment
  }
}

resource "aws_security_group" "rds" {
  count = var.create_rds ? 1 : 0

  name        = "corpus-${var.corpus_id}-${var.environment}-rds"
  description = "PostgreSQL access for corpus deployment"
  vpc_id      = var.vpc_id
}

resource "aws_vpc_security_group_ingress_rule" "rds_postgres" {
  count = var.create_rds ? 1 : 0

  security_group_id = aws_security_group.rds[0].id

  description                  = "PostgreSQL from ECS service"
  from_port                    = 5432
  to_port                      = 5432
  ip_protocol                  = "tcp"
  referenced_security_group_id = aws_security_group.service.id
}

# ==============================================================================
# IAM
# ==============================================================================

resource "aws_iam_role" "service_task" {
  name = "corpus-${var.corpus_id}-${var.environment}-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
        Action = "sts:AssumeRole"
      }
    ]
  })
}

resource "aws_iam_role_policy" "service_s3" {
  name = "corpus-${var.corpus_id}-${var.environment}-s3"
  role = aws_iam_role.service_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:ListBucket"
        ]
        Resource = [
          aws_s3_bucket.artifacts.arn,
          "${aws_s3_bucket.artifacts.arn}/*"
        ]
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "service_task_execution" {
  role       = aws_iam_role.service_task.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# ==============================================================================
# Networking
# ==============================================================================

resource "aws_security_group" "service" {
  name        = "corpus-${var.corpus_id}-${var.environment}-service"
  description = "Query service and MCP adapter"
  vpc_id      = var.vpc_id
}

resource "aws_vpc_security_group_ingress_rule" "service_https" {
  security_group_id = aws_security_group.service.id

  description                  = "HTTPS from ALB"
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
  referenced_security_group_id = aws_security_group.alb.id
}

resource "aws_vpc_security_group_egress_rule" "service_all" {
  security_group_id = aws_security_group.service.id

  description = "Allow all outbound"
  ip_protocol = "-1"
  cidr_ipv4   = "0.0.0.0/0"
}

resource "aws_security_group" "alb" {
  name        = "corpus-${var.corpus_id}-${var.environment}-alb"
  description = "ALB for corpus deployment"
  vpc_id      = var.vpc_id
}

resource "aws_vpc_security_group_ingress_rule" "alb_https" {
  security_group_id = aws_security_group.alb.id

  description = "HTTPS from internet"
  from_port   = 443
  to_port     = 443
  ip_protocol = "tcp"
  cidr_ipv4   = "0.0.0.0/0"
}

resource "aws_vpc_security_group_egress_rule" "alb_service" {
  security_group_id = aws_security_group.alb.id

  description                  = "Forward to service"
  from_port                    = 8080
  to_port                      = 8080
  ip_protocol                  = "tcp"
  referenced_security_group_id = aws_security_group.service.id
}

# ==============================================================================
# Application Load Balancer
# ==============================================================================

resource "aws_lb" "service" {
  name               = "corpus-${replace(var.corpus_id, "_", "-")}-${var.environment}"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = var.alb_subnet_ids
}

resource "aws_lb_target_group" "service" {
  name        = "corpus-${replace(var.corpus_id, "_", "-")}-${var.environment}"
  port        = 8080
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = var.vpc_id

  health_check {
    path                = "/"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
    matcher             = "200-404"
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.service.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.service.arn
  }
}

# ==============================================================================
# ECS Fargate Service
# ==============================================================================

resource "aws_ecs_cluster" "service" {
  name = "corpus-${var.corpus_id}-${var.environment}"
}

resource "aws_ecs_task_definition" "service" {
  family                   = "corpus-${var.corpus_id}-${var.environment}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = var.service_cpu
  memory                   = var.service_memory
  execution_role_arn       = aws_iam_role.service_task.arn
  task_role_arn            = aws_iam_role.service_task.arn

  container_definitions = jsonencode([
    {
      name  = "query-service"
      image = "${var.container_image_repository}:query-service-${var.image_tag}"
      portMappings = [
        {
          containerPort = 5000
          protocol      = "tcp"
        }
      ]
      environment = [
        { name = "CORPUS_DEPLOYMENT_NAME", value = var.deployment_name },
        { name = "CORPUS_ID", value = var.corpus_id },
        { name = "AUTH_TOKEN", value = var.auth_token },
        { name = "STORAGE_POSTGRES_CONNECTION_STRING", value = var.postgres_connection_string },
        { name = "STORAGE_S3_BUCKET_NAME", value = aws_s3_bucket.artifacts.id },
        { name = "DATASET_ALLOW_VERSION_PINNING", value = tostring(var.allow_version_pinning) }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.service.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "query-service"
        }
      }
    },
    {
      name  = "mcp-adapter"
      image = "${var.container_image_repository}:mcp-adapter-${var.image_tag}"
      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]
      environment = [
        { name = "CORPUS_DEPLOYMENT_NAME", value = var.deployment_name },
        { name = "CORPUS_ID", value = var.corpus_id },
        { name = "AUTH_TOKEN", value = var.auth_token },
        { name = "QUERY_SERVICE_URL", value = "http://localhost:5000" }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.service.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "mcp-adapter"
        }
      }
    }
  ])
}

resource "aws_ecs_service" "service" {
  name            = "corpus-${var.corpus_id}-${var.environment}"
  cluster         = aws_ecs_cluster.service.id
  task_definition = aws_ecs_task_definition.service.arn
  desired_count   = var.service_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.service_subnet_ids
    security_groups  = [aws_security_group.service.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.service.arn
    container_name   = "mcp-adapter"
    container_port   = 8080
  }

  depends_on = [aws_lb_listener.https]
}

# ==============================================================================
# CloudWatch Logs
# ==============================================================================

resource "aws_cloudwatch_log_group" "service" {
  name              = "/corpus/${var.corpus_id}/${var.environment}"
  retention_in_days = var.log_retention_days
}

# ==============================================================================
# SSM Parameter Store — runtime configuration (tokens, connection strings)
# ==============================================================================

resource "aws_ssm_parameter" "auth_token" {
  name        = "/corpus/${var.corpus_id}/${var.environment}/AUTH_TOKEN"
  description = "Static bearer token for API access"
  type        = "SecureString"
  value       = var.auth_token
}

resource "aws_ssm_parameter" "postgres_connection_string" {
  name        = "/corpus/${var.corpus_id}/${var.environment}/POSTGRES_CONNECTION_STRING"
  description = "PostgreSQL connection string for the serving store"
  type        = "SecureString"
  value       = var.postgres_connection_string
}
