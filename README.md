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

## Cloud Run and Cloud SQL

Build and deploy the image, attach the Cloud SQL PostgreSQL instance, and store
the database password in Secret Manager. Set `ConnectionStrings__PaymentDb` to
the Cloud SQL private-IP endpoint (or connector endpoint), `PublicBaseUrl` to the
Cloud Run service URL, `Auth__Mode`, and `PaymentProvider__Type=Fake`. Cloud Run
injects `PORT`; the application binds to `0.0.0.0:$PORT`. Run migrations as a
Cloud Run Job before shifting traffic. Keep minimum instances at zero for a POC,
but use connection pooling and an appropriate maximum pool size in production.

## Test checkout and callbacks

Open the `nextUrl` from the card-payment response and choose success, failure,
or cancel. A transition is append-only in status history and terminal states
cannot be changed. The callback receives `{ paymentReference, status }`. Delivery
is best effort and happens after the database commit, so network failure never
rolls back payment state.
