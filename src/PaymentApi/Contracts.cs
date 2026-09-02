using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PaymentApi;

// Request DTOs use property-based classes so MVC reads validation and JSON
// metadata from one unambiguous location. Positional records split that metadata
// between constructor parameters and generated properties.
public sealed class CreateServiceRequest
{
    [Required, JsonPropertyName("callBackUrl")]
    public string CallBackUrl { get; init; } = null!;

    [JsonPropertyName("caseReference")]
    public string? CaseReference { get; init; }

    [Required, JsonPropertyName("ccdCaseNumber")]
    public string CcdCaseNumber { get; init; } = null!;

    [Required, MinLength(1), JsonPropertyName("fees")]
    public IReadOnlyList<CreateFee> Fees { get; init; } = null!;
}

public sealed class CreateLegacyServiceRequest
{
    [Required, JsonPropertyName("legacySystem")]
    public string LegacySystem { get; init; } = null!;

    [Required, JsonPropertyName("transactionId")]
    public string TransactionId { get; init; } = null!;

    [Required, JsonPropertyName("callBackUrl")]
    public string CallBackUrl { get; init; } = null!;

    [JsonPropertyName("caseReference")]
    public string? CaseReference { get; init; }

    [Required, JsonPropertyName("ccdCaseNumber")]
    public string CcdCaseNumber { get; init; } = null!;

    [Required, MinLength(1), JsonPropertyName("fees")]
    public IReadOnlyList<CreateFee> Fees { get; init; } = null!;
}

public sealed class CreateFee
{
    [Required, JsonPropertyName("code")]
    public string Code { get; init; } = null!;

    [Required, JsonPropertyName("version")]
    public string Version { get; init; } = null!;

    [Range(typeof(decimal), "0.01", "999999999"), JsonPropertyName("calculatedAmount")]
    public decimal CalculatedAmount { get; init; }
}

public sealed class CreateCardPayment
{
    [Required, RegularExpression("GBP"), JsonPropertyName("currency")]
    public string Currency { get; init; } = null!;

    [Range(typeof(decimal), "0.01", "999999999"), JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [Required, Url, JsonPropertyName("returnUrl")]
    public string ReturnUrl { get; init; } = null!;

    [RegularExpression("en|cy"), JsonPropertyName("language")]
    public string? Language { get; init; }
}

public sealed class CreateLegacyPayment
{
    [Required, JsonPropertyName("legacySystem")]
    public string LegacySystem { get; init; } = null!;

    [Required, JsonPropertyName("transactionId")]
    public string TransactionId { get; init; } = null!;

    [Required, JsonPropertyName("legacyPaymentReference")]
    public string LegacyPaymentReference { get; init; } = null!;

    [Required, JsonPropertyName("currency")]
    public string Currency { get; init; } = null!;

    [Range(typeof(decimal), "0.01", "999999999"), JsonPropertyName("amount")]
    public decimal Amount { get; init; }
}

public sealed record FeeResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("calculatedAmount")] decimal CalculatedAmount);

public sealed record ServiceRequestResponse(
    [property: JsonPropertyName("serviceRequestReference")] string ServiceRequestReference,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset DateCreated,
    [property: JsonPropertyName("fees")] IReadOnlyList<FeeResponse> Fees);

public sealed record LegacyServiceRequestResponse(
    [property: JsonPropertyName("serviceRequestReference")] string ServiceRequestReference,
    [property: JsonPropertyName("dateCreated")] DateTimeOffset DateCreated,
    [property: JsonPropertyName("fees")] IReadOnlyList<FeeResponse> Fees);

public sealed record CardPaymentResponse(
    [property: JsonPropertyName("paymentReference")] string PaymentReference,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("nextUrl")] string NextUrl);

public sealed record LegacyPaymentResponse(
    [property: JsonPropertyName("paymentReference")] string PaymentReference,
    [property: JsonPropertyName("status")] string Status);

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
