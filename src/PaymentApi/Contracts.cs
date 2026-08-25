using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentApi;

public sealed record CreateServiceRequest(
    [property: Required, JsonPropertyName("callBackUrl")] string CallBackUrl,
    [property: JsonPropertyName("caseReference")] string? CaseReference,
    [property: Required, JsonPropertyName("ccdCaseNumber")] string CcdCaseNumber,
    [property: Required, MinLength(1), JsonPropertyName("fees")] IReadOnlyList<CreateFee> Fees);

public sealed record CreateFee(
    [property: Required, JsonPropertyName("code")] string Code,
    [property: Required, JsonPropertyName("version")] string Version,
    [property: Range(typeof(decimal), "0.01", "999999999"), JsonPropertyName("calculatedAmount")] decimal CalculatedAmount);

public sealed record CreateCardPayment(
    [property: Required, RegularExpression("GBP"), JsonPropertyName("currency")] string Currency,
    [property: Range(typeof(decimal), "0.01", "999999999"), JsonPropertyName("amount")] decimal Amount,
    [property: Required, Url, JsonPropertyName("returnUrl")] string ReturnUrl,
    [property: RegularExpression("en|cy"), JsonPropertyName("language")] string? Language);

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

public sealed record ErrorResponse([property: JsonPropertyName("error")] string Error);
