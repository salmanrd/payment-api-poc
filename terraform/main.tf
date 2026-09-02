locals {
  # Keep all regional application resources in Google's London region.
  gcp_region = "europe-west2"

  required_services = toset([
    "artifactregistry.googleapis.com",
    "run.googleapis.com",
    "secretmanager.googleapis.com",
  ])
}

resource "google_project_service" "required" {
  for_each = local.required_services

  project            = var.project_id
  service            = each.value
  disable_on_destroy = false
}

resource "google_artifact_registry_repository" "app" {
  project       = var.project_id
  location      = local.gcp_region
  repository_id = var.service_name
  description   = "Container images for ${var.service_name}"
  format        = "DOCKER"

  depends_on = [google_project_service.required]
}

data "google_secret_manager_secret" "database" {
  project   = var.project_id
  secret_id = var.database_secret_id

  depends_on = [google_project_service.required]
}

resource "google_service_account" "app" {
  project      = var.project_id
  account_id   = var.service_name
  display_name = "Cloud Run identity for ${var.service_name}"
}

resource "google_secret_manager_secret_iam_member" "database_accessor" {
  project   = var.project_id
  secret_id = data.google_secret_manager_secret.database.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.app.email}"
}

resource "google_cloud_run_v2_service" "app" {
  project             = var.project_id
  name                = var.service_name
  location            = local.gcp_region
  deletion_protection = false

  template {
    service_account = google_service_account.app.email

    scaling {
      min_instance_count = var.min_instance_count
      max_instance_count = var.max_instance_count
    }

    containers {
      image = var.container_image

      ports {
        container_port = 8080
      }

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "Auth__Mode"
        value = "None"
      }

      env {
        name  = "PaymentProvider__Type"
        value = "Fake"
      }

      env {
        name  = "PublicBaseUrl"
        value = var.public_base_url
      }

      env {
        name = "ConnectionStrings__PaymentDb"
        value_source {
          secret_key_ref {
            secret  = data.google_secret_manager_secret.database.secret_id
            version = var.database_secret_version
          }
        }
      }

      resources {
        limits = {
          cpu    = "1"
          memory = "512Mi"
        }
      }

      startup_probe {
        initial_delay_seconds = 1
        timeout_seconds       = 1
        period_seconds        = 3
        failure_threshold     = 20

        http_get {
          path = "/health"
          port = 8080
        }
      }

      liveness_probe {
        timeout_seconds   = 1
        period_seconds    = 10
        failure_threshold = 3

        http_get {
          path = "/health"
          port = 8080
        }
      }
    }
  }

  depends_on = [
    google_project_service.required,
    google_secret_manager_secret_iam_member.database_accessor,
  ]
}

resource "google_cloud_run_v2_service_iam_member" "public" {
  count = var.allow_unauthenticated ? 1 : 0

  project  = google_cloud_run_v2_service.app.project
  location = google_cloud_run_v2_service.app.location
  name     = google_cloud_run_v2_service.app.name
  role     = "roles/run.invoker"
  member   = "allUsers"
}
