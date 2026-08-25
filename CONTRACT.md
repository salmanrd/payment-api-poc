# compatibility notes

The implementation target is `ccpay-payment-app`: the service-request and
card-payment controllers, their DTO constraints and exception mapping, and the
service calls they make. Direct GitHub access was unavailable in the build
environment (the CONNECT tunnel returned HTTP 403), so no claim of byte-for-byte
parity is made. This is the pre-coding contract-difference record requested for
the POC.

Material deliberate differences are: external payment provider is
replaced by a deterministic fake checkout; authentication can be disabled or
reduced to presence checks for the `Authorization` and `ServiceAuthorization`
headers; references use the same `SR-`/`RC-` categories but are locally generated;
and only the two selected write operations are exposed. JSON casing, routes,
GBP/amount/URL validation, 201/400/401/404 outcomes, active-payment idempotency,
and terminal-state protection are retained. Callback failure is intentionally
best effort after committing the terminal state.
