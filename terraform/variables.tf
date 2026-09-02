variable "project_id" {
  description = "Google Cloud project in which to deploy the application."
  type        = string
}

variable "service_name" {
  description = "Name of the Cloud Run service and its runtime service account."
  type        = string
  default     = "payment-api"
}

variable "container_image" {
  description = "Immutable container image reference (preferably an Artifact Registry digest) to deploy."
  type        = string
}

variable "database_secret_id" {
  description = "Project-local Secret Manager secret ID containing the complete Supabase PostgreSQL connection string."
  type        = string
  default     = "payment-db-connection-string"
}

variable "database_secret_version" {
  description = "Secret version exposed to Cloud Run. Pin a numeric version in production; 'latest' is convenient for a POC."
  type        = string
  default     = "latest"
}

variable "public_base_url" {
  description = "Externally reachable origin used to generate checkout URLs (for example, the existing Cloud Run URL or a custom domain)."
  type        = string

  validation {
    condition     = can(regex("^https://", var.public_base_url)) && !endswith(var.public_base_url, "/")
    error_message = "public_base_url must be an HTTPS origin without a trailing slash."
  }
}

variable "allow_unauthenticated" {
  description = "Whether to grant the public permission to invoke the Cloud Run service."
  type        = bool
  default     = true
}

variable "min_instance_count" {
  description = "Minimum number of warm Cloud Run instances."
  type        = number
  default     = 0
}

variable "max_instance_count" {
  description = "Maximum number of Cloud Run instances, limiting concurrent database connection growth."
  type        = number
  default     = 3

  validation {
    condition     = var.max_instance_count >= var.min_instance_count && var.max_instance_count > 0
    error_message = "max_instance_count must be positive and at least min_instance_count."
  }
}
