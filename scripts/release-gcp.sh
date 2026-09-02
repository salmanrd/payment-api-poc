#!/usr/bin/env bash

set -Eeuo pipefail

readonly GCP_REGION="europe-west2"
readonly ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly TERRAFORM_DIR="${ROOT_DIR}/terraform"
readonly TFVARS_FILE="${TERRAFORM_DIR}/terraform.tfvars"

fail() {
  printf 'error: %s\n' "$*" >&2
  exit 1
}

for command in git gcloud terraform; do
  command -v "${command}" >/dev/null 2>&1 || fail "${command} is required but was not found"
done

[[ -f "${TFVARS_FILE}" ]] || fail "${TFVARS_FILE} is missing; copy terraform.tfvars.example and configure it first"

cd "${ROOT_DIR}"

[[ -z "$(git status --porcelain --untracked-files=normal)" ]] || fail "the Git working tree must be clean before releasing"

PROJECT_ID="${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}"
[[ -n "${PROJECT_ID}" && "${PROJECT_ID}" != "(unset)" ]] || fail "set GCP_PROJECT_ID or select a project with 'gcloud config set project PROJECT_ID'"

SERVICE_NAME="${SERVICE_NAME:-payment-api}"
IMAGE_TAG="${1:-$(git rev-parse HEAD)}"
[[ "${IMAGE_TAG}" =~ ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$ ]] || fail "image tag '${IMAGE_TAG}' is not a valid container tag"
IMAGE="${GCP_REGION}-docker.pkg.dev/${PROJECT_ID}/${SERVICE_NAME}/${SERVICE_NAME}:${IMAGE_TAG}"
PLAN_FILE="$(mktemp "${TMPDIR:-/tmp}/payment-api-release.XXXXXX.tfplan")"
trap 'rm -f "${PLAN_FILE}"' EXIT

printf 'Releasing commit %s to project %s\n' "$(git rev-parse --short HEAD)" "${PROJECT_ID}"
printf 'Container image: %s\n' "${IMAGE}"

terraform -chdir="${TERRAFORM_DIR}" init -input=false

terraform -chdir="${TERRAFORM_DIR}" state show google_artifact_registry_repository.app >/dev/null 2>&1 ||
  fail "Artifact Registry is not in Terraform state; complete the documented first-deployment bootstrap"

gcloud builds submit "${ROOT_DIR}" \
  --project="${PROJECT_ID}" \
  --region="${GCP_REGION}" \
  --tag="${IMAGE}"

TF_VAR_project_id="${PROJECT_ID}" \
TF_VAR_container_image="${IMAGE}" \
  terraform -chdir="${TERRAFORM_DIR}" plan \
    -input=false \
    -out="${PLAN_FILE}"

terraform -chdir="${TERRAFORM_DIR}" apply -input=false "${PLAN_FILE}"

printf '\nRelease complete.\nService URL: '
terraform -chdir="${TERRAFORM_DIR}" output -raw service_url
printf '\nImage: %s\n' "${IMAGE}"
