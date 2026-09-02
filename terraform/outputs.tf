output "service_url" {
  description = "Cloud Run URL for the deployed web application."
  value       = google_cloud_run_v2_service.app.uri
}

output "artifact_registry_repository" {
  description = "Artifact Registry repository to which application images can be pushed."
  value       = "${local.gcp_region}-docker.pkg.dev/${var.project_id}/${google_artifact_registry_repository.app.repository_id}"
}

output "deployment_region" {
  description = "UK Google Cloud region containing the application resources."
  value       = local.gcp_region
}

output "runtime_service_account" {
  description = "Least-privilege identity used by Cloud Run."
  value       = google_service_account.app.email
}
