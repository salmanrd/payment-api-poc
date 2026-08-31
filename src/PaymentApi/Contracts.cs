using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentApi;

public sealed record CreateServiceRequest(
    [param: Required]
    [property: JsonPropertyName("callBackUrl")] string CallBackUrl,
    [property: JsonPropertyName("caseReference")] string? CaseReference,
    [param: Required]
    [property: JsonPropertyName("ccdCaseNumber")] string CcdCaseNumber,
    [param: Required, MinLength(1)]
    [property: JsonPropertyName("fees")] IReadOnlyList<CreateFee> Fees);

public sealed record CreateFee(
    [param: Required]
    [property: JsonPropertyName("code")] string Code,
    [param: Required]
    [property: JsonPropertyName("version")] string Version,
    [param: Range(typeof(decimal), "0.01", "999999999")]
    [property: JsonPropertyName("calculatedAmount")] decimal CalculatedAmount);

public sealed record CreateCardPayment(
    [param: Required, RegularExpression("GBP")]
    [property: JsonPropertyName("currency")] string Currency,
    [param: Range(typeof(decimal), "0.01", "999999999")]
    [property: JsonPropertyName("amount")] decimal Amount,
    [param: Required, Url]
    [property: JsonPropertyName("returnUrl")] string ReturnUrl,
    [param: RegularExpression("en|cy")]
    [property: JsonPropertyName("language")] string? Language);

public sealed record FeeResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("calculatedAmount")] decimal CalculatedAmount);

public sealed record ServiceRequestResponse(
    [property: JsonPropertyName("serviceRequestReference")] string ServiceRequestReference,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset DateCreated,
    [property: JsonPropertyName("fees")] IReadOnlyList<FeeResponse> Fees);

public sealed record CardPaymentResponse(
    [property: JsonPropertyName("paymentReference")] string PaymentReference,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("nextUrl")] string NextUrl);

public sealed record PaymentReadResponse(
    [property: JsonPropertyName("paymentReference")] string PaymentReference,
    [property: JsonPropertyName("serviceRequestReference")] string ServiceRequestReference,
    [property: JsonPropertyName("caseReference")] string? CaseReference,
    [property: JsonPropertyName("ccdCaseNumber")] string CcdCaseNumber,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset DateCreated);

public sealed record ErrorResponse([property: JsonPropertyName("error")] string Error);
