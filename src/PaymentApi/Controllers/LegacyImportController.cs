using Microsoft.AspNetCore.Mvc;

namespace PaymentApi.Controllers;

[ApiController]
[Tags("Legacy import")]
public sealed class LegacyImportController(PaymentService payments, IConfiguration configuration) : ControllerBase
{
    [HttpPost("/legacy-service-request")]
    [ProducesResponseType<LegacyServiceRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<LegacyServiceRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateLegacyServiceRequest request, CancellationToken cancellationToken)
    {
        if (!IsMigrationAuthorized()) return Unauthorized(new ErrorResponse("Unauthorized"));

        if (!Uri.TryCreate(request.CallBackUrl, UriKind.Absolute, out var callback) ||
            callback.Scheme is not ("http" or "https"))
        {
            ModelState.AddModelError("callBackUrl",
                "The callBackUrl field is not a valid fully-qualified HTTP or HTTPS URL.");
            return ValidationProblem(ModelState);
        }

        var result = await payments.CreateLegacyServiceRequest(request, cancellationToken);
        if (result.ServiceRequest is null)
        {
            var error = new ErrorResponse(result.Error!);
            return result.Error == "Archived transaction not found" ? NotFound(error) :
                result.Error!.StartsWith("A materially different", StringComparison.Ordinal) ? Conflict(error) : BadRequest(error);
        }

        var response = new LegacyServiceRequestResponse(result.ServiceRequest.Reference, result.ServiceRequest.Created,
            result.ServiceRequest.Fees.Select(x => new FeeResponse(x.Code, x.Version, x.Amount)).ToList());
        return result.Created
            ? Created($"/service-request/{result.ServiceRequest.Reference}", response)
            : Ok(response);
    }

    private bool IsMigrationAuthorized()
    {
        if (!string.Equals(configuration["Auth:Mode"], "Mock", StringComparison.OrdinalIgnoreCase)) return true;
        return Request.Headers.TryGetValue("Authorization", out var user) && !string.IsNullOrWhiteSpace(user) &&
               Request.Headers.TryGetValue("ServiceAuthorization", out var service) && !string.IsNullOrWhiteSpace(service) &&
               Request.Headers.TryGetValue("MigrationAuthorization", out var migration) && !string.IsNullOrWhiteSpace(migration);
    }
}
