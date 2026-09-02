# compatible payment API POC

.NET 8 ASP.NET Core controller API implementing `POST /service-request` and `POST
/service-request/{service-request-reference}/card-payments`. Swagger is at
`/swagger`, liveness at `/health`, and the fake provider checkout at the returned
`nextUrl`. See [CONTRACT.md](CONTRACT.md) for the compatibility scope.

The lightweight payments interface is available at `/payments-ui`. It lists
payments with search and status filters and links to payment details. The same
application also exposes the read-only JSON routes `GET /payments` and
`GET /payments/{paymentReference}` used by this POC's payment views.

Create a service request from `/service-requests/new`. The form supports one or
more fees and submits them to the `POST /service-request` API.

## Creating a service request through the API

`GET /payments` is a read-only endpoint and does not prove that the database is
writable. `POST /service-request` requires a JSON body and, when
`Auth__Mode=Mock`, both authentication headers. For example:

```sh
curl --fail-with-body -i http://localhost:5080/service-request \
  -H 'Content-Type: application/json' \
  -H 'Authorization: Bearer local-test' \
  -H 'ServiceAuthorization: local-test' \
  --data '{
    "callBackUrl": "https://example.test/payment-status",
    "caseReference": "case-123",
    "ccdCaseNumber": "1234567890123456",
    "fees": [
      { "code": "FEE0001", "version": "1", "calculatedAmount": 10.00 }
    ]
  }'
```

The expected response is `201 Created`. A `400` response means the JSON failed
request validation, and `401` means the headers required by mock authentication
are absent. A `500` is not an authentication or payload-validation response; it
usually means that the configured database cannot execute the lookup or insert.
Check the application log for the underlying PostgreSQL error, verify that
`ConnectionStrings__PaymentDb` points to the intended writable database, and
apply the migration with:

```sh
dotnet ef database update --project src/PaymentApi
```

Run the migration against the same connection string and environment used by
the deployed application. In hosted environments this should normally be a
one-off deployment job rather than an operation performed on every application
startup.

The controller has ASP.NET Core's `[ApiController]` behavior enabled. Body
deserialization, content-type checks, and data-annotation validation therefore
happen **before** `CreateServiceRequest` is invoked. If a breakpoint at the
start of that method is not reached, inspect the raw HTTP status and response
body first:

* `400 application/problem+json` means the JSON could not be deserialized or a
  required value/fee failed validation. The `errors` object identifies the
  rejected field.
* `415 Unsupported Media Type` means the request was not sent with
  `Content-Type: application/json`.
* `401` can only be returned from inside this action, so reaching it confirms
  that binding and automatic validation completed.
* The generic `500` JSON response is produced by the exception handler. Check
  the server log for that request; an exception raised before action invocation
  cannot be diagnosed from the intentionally generic public response.

When debugging, use `curl -i` (as above), the browser Network panel, or disable
the client's "throw on non-success" behavior so the actual status and Problem
Details response are not replaced by a generic client-side error.

## Local PostgreSQL

1. Install .NET 8 and PostgreSQL 14 or newer.
2. Copy `.env.example` to `.env`, replace the password, and export the variables.
3. Create the database, then run `dotnet ef database update --project src/PaymentApi`.
4. Run `dotnet run --project src/PaymentApi`; the local launch profile listens on
   `http://localhost:5080`.

For local runs, set `ASPNETCORE_URLS` to choose another address. In hosted
environments, setting `PORT` binds the application to `0.0.0.0:$PORT` (the
container sets it to `8080`). If startup reports that an address is already in
use, either stop the process using that port or select a free local port, for
example:

```sh
ASPNETCORE_URLS=http://localhost:5081 \
PublicBaseUrl=http://localhost:5081 \
dotnet run --project src/PaymentApi --no-launch-profile
```

`Auth__Mode=None` disables header checks. `Mock` requires non-empty
`Authorization` and `ServiceAuthorization` headers. Only
`PaymentProvider__Type=Fake` is supported. `PublicBaseUrl` must be the externally
reachable API origin.

## Supabase

Use the Supabase direct or session-pooler PostgreSQL connection details in
`ConnectionStrings__PaymentDb`. Append `SSL Mode=Require;Trust Server
Certificate=true`, run the migration once, and never commit the credential.
There are no provider-specific extensions, so the same migration works on plain
PostgreSQL and Cloud SQL.

## Docker

```sh
docker build -t payment-api .
docker run --rm -p 8080:8080 --env-file .env payment-api
```

For a database on the host, use `host.docker.internal` (Docker Desktop) as the
connection host. On Linux, attach both containers to one Docker network.

## Deploying the web app to Google Cloud with Terraform

The configuration in [`terraform/`](terraform/) deploys only the web app to a
Cloud Run service in the UK (`europe-west2`, London). The region is fixed in the
configuration so an environment-specific variable cannot accidentally move the
service or its Artifact Registry repository outside the UK. PostgreSQL remains
in Supabase. Terraform creates the repository and a dedicated runtime service
account, then gives that identity access to one existing Secret Manager secret.
It does not put the database credential in Terraform configuration or state.

Prerequisites are Terraform 1.6+, the Google Cloud CLI, a GCP project with
billing enabled, and permission to enable APIs and manage Cloud Run, Artifact
Registry, service accounts, and secret IAM. Authenticate Application Default
Credentials and select the project:

```sh
gcloud auth application-default login
gcloud config set project MY_PROJECT
```

Create the secret before applying Terraform. Its value must be the **complete**
Supabase connection string, including TLS options. Do not pass only a password:

```sh
printf '%s' 'Host=...;Port=5432;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true' \
  | gcloud secrets create payment-db-connection-string \
      --replication-policy=automatic --data-file=-
```

Build the image and push it to the repository path that Terraform will create.
On the first deployment, create just the repository before pushing, then apply
the complete configuration:

```sh
cp terraform/terraform.tfvars.example terraform/terraform.tfvars
# Edit terraform.tfvars, especially project_id, container_image, and public_base_url.
terraform -chdir=terraform init
terraform -chdir=terraform apply -target=google_artifact_registry_repository.app
gcloud auth configure-docker europe-west2-docker.pkg.dev
docker build -t europe-west2-docker.pkg.dev/MY_PROJECT/payment-api/payment-api:TAG .
docker push europe-west2-docker.pkg.dev/MY_PROJECT/payment-api/payment-api:TAG
terraform -chdir=terraform apply
```

Cloud Run resolves the selected secret version directly into
`ConnectionStrings__PaymentDb` at container startup; the application already
reads that standard ASP.NET Core configuration key. The runtime identity has
access only to the selected database secret. Prefer an immutable image digest
and a numeric `database_secret_version` for reproducible production deploys.

`public_base_url` must be the external HTTPS origin used in generated checkout
links. If using the default Cloud Run URL for the first deployment, apply once
with a temporary HTTPS origin, copy `service_url` from the Terraform output into
`public_base_url`, and apply again. A custom domain avoids that bootstrap step.

Apply EF Core migrations to Supabase as a separate, one-off release step before
sending traffic to a schema-dependent version; the web service deliberately
does not migrate the shared database on startup.

### Releasing subsequent application changes

After the first deployment is complete, the release script builds the current
Git commit with Cloud Build, pushes it to the London Artifact Registry, and
deploys that exact image with Terraform:

```sh
./scripts/release-gcp.sh
```

The script requires a clean Git working tree, an existing
`terraform/terraform.tfvars`, authenticated `gcloud` and Terraform access, and
the Artifact Registry repository from the first-deployment bootstrap. It uses
the configured `gcloud` project by default. To select one explicitly:

```sh
GCP_PROJECT_ID=MY_PROJECT ./scripts/release-gcp.sh
```

Every release uses the full Git commit SHA as its container tag. An explicit
tag can be supplied as the first argument when necessary:

```sh
./scripts/release-gcp.sh release-2026-09-02
```

Run any required EF Core migrations against Supabase before invoking the
script. The script deliberately never reads the database credential or runs
migrations from the operator's machine.

## Test checkout and callbacks

Open the `nextUrl` from the card-payment response and choose success, failure,
or cancel. A transition is append-only in status history and terminal states
cannot be changed. The callback receives `{ paymentReference, status }`. Delivery
is best effort and happens after the database commit, so network failure never
rolls back payment state.
