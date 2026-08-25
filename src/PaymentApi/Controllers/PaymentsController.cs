using Microsoft.AspNetCore.Mvc;

namespace PaymentApi.Controllers;

[ApiController]
[Tags("Payments")]
public sealed class PaymentsController(
    PaymentService payments,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("/service-request")]
    [ProducesResponseType<ServiceRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateServiceRequest(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
            return Unauthorized(new ErrorResponse("Unauthorized"));

        if (!Uri.TryCreate(request.CallBackUrl, UriKind.Absolute, out var callback) ||
            callback.Scheme is not ("http" or "https"))
        {
            ModelState.AddModelError("callBackUrl",
                "The callBackUrl field is not a valid fully-qualified HTTP or HTTPS URL.");
            return ValidationProblem(ModelState);
        }

        var serviceRequest = await payments.CreateServiceRequest(request, cancellationToken);
        var response = new ServiceRequestResponse(
            serviceRequest.Reference,
            serviceRequest.Created,
            serviceRequest.Fees.Select(fee =>
                new FeeResponse(fee.Code, fee.Version, fee.Amount)).ToList());

        return Created($"/service-request/{serviceRequest.Reference}", response);
    }

    [HttpPost("/service-request/{service-request-reference}/card-payments")]
    [ProducesResponseType<CardPaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCardPayment(
        [FromRoute(Name = "service-request-reference")] string serviceRequestReference,
        [FromBody] CreateCardPayment request,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized())
            return Unauthorized(new ErrorResponse("Unauthorized"));

        var result = await payments.CreatePayment(
            serviceRequestReference, request, cancellationToken);

        if (result.Payment is null)
        {
            var error = new ErrorResponse(result.Error!);
            return result.Error == "Service request not found"
                ? NotFound(error)
                : BadRequest(error);
        }

        return Created(
            $"/card-payments/{result.Payment.Reference}",
            payments.Response(result.Payment));
    }

    private bool IsAuthorized()
    {
        if (!string.Equals(configuration["Auth:Mode"], "Mock",
                StringComparison.OrdinalIgnoreCase))
            return true;

        return Request.Headers.TryGetValue("Authorization", out var authorization) &&
               !string.IsNullOrWhiteSpace(authorization) &&
               Request.Headers.TryGetValue("ServiceAuthorization", out var serviceAuthorization) &&
               !string.IsNullOrWhiteSpace(serviceAuthorization);
    }
}
