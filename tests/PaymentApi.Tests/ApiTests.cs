using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentApi;
using Xunit;

namespace PaymentApi.Tests;

public sealed class Factory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
    {
        var descriptor = services.Single(x => x.ServiceType == typeof(DbContextOptions<PaymentDbContext>)); services.Remove(descriptor);
        services.AddDbContext<PaymentDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
    });
}
public sealed class ApiTests(Factory factory) : IClassFixture<Factory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    private static object Sr(string ccd = "1234567890123456") => new { callBackUrl = "https://example.test/callback", caseReference = "case", ccdCaseNumber = ccd, fees = new[] { new { code = "FEE0001", version = "1", calculatedAmount = 10.00m } } };
    private async Task<string> CreateSr(string? ccd = null) { var r = await client.PostAsJsonAsync("/service-request", Sr(ccd ?? Random.Shared.NextInt64(1_000_000_000_000_000, 9_999_999_999_999_999).ToString())); return (await r.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference; }
    [Fact] public async Task Health_is_up() => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    [Fact]
    public async Task Transaction_can_be_persisted_with_payment_references()
    {
        var transactionId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.Transactions.Add(new TransactionEntity
            {
                TransactionId = transactionId,
                CaseNo = "1234567890123456",
                TransactionType = "Payment",
                TransactionMethodId = 1,
                TransactionDate = DateTimeOffset.UtcNow,
                Amount = 10m,
                TransactionStatus = "Success",
                OriginalPaymentReference = "RC-ORIGINAL",
                PaymentReference = "RC-CURRENT"
            });
            await db.SaveChangesAsync();
        }

        using var checkScope = factory.Services.CreateScope();
        var saved = await checkScope.ServiceProvider.GetRequiredService<PaymentDbContext>()
            .Transactions.AsNoTracking().SingleAsync(x => x.TransactionId == transactionId);
        Assert.Equal("1234567890123456", saved.CaseNo);
        Assert.Equal("Payment", saved.TransactionType);
        Assert.Equal(1, saved.TransactionMethodId);
        Assert.Equal(10m, saved.Amount);
        Assert.Equal("Success", saved.TransactionStatus);
        Assert.Equal("RC-ORIGINAL", saved.OriginalPaymentReference);
        Assert.Equal("RC-CURRENT", saved.PaymentReference);
    }
    [Fact] public async Task Service_request_is_created() { var r = await client.PostAsJsonAsync("/service-request", Sr()); Assert.Equal(HttpStatusCode.Created, r.StatusCode); Assert.StartsWith("SR-", (await r.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference); }
    [Fact] public async Task Multiple_service_requests_can_use_the_same_ccd_case() { var ccd = Random.Shared.NextInt64().ToString(); var a = await client.PostAsJsonAsync("/service-request", Sr(ccd)); var b = await client.PostAsJsonAsync("/service-request", Sr(ccd)); Assert.NotEqual((await a.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference, (await b.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference); }
    [Fact]
    public async Task Legacy_service_request_is_validated_and_idempotent()
    {
        var transactionId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.ArchivedTransactions.Add(new ArchivedTransactionEntity { Id = Guid.NewGuid(), LegacySystem = "fees-v1", TransactionId = transactionId, TransactionType = "Fee", CcdCaseNumber = "123", CaseReference = "case", FeeTotal = 10m });
            await db.SaveChangesAsync();
        }
        var body = new { legacySystem = "fees-v1", transactionId, callBackUrl = "https://example.test/callback", caseReference = "case", ccdCaseNumber = "123", fees = new[] { new { code = "FEE1", version = "1", calculatedAmount = 10m } } };
        var first = await client.PostAsJsonAsync("/legacy-service-request", body);
        var retry = await client.PostAsJsonAsync("/legacy-service-request", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<LegacyServiceRequestResponse>())!.ServiceRequestReference,
            (await retry.Content.ReadFromJsonAsync<LegacyServiceRequestResponse>())!.ServiceRequestReference);
        using var checkScope = factory.Services.CreateScope();
        var details = await checkScope.ServiceProvider.GetRequiredService<PaymentDbContext>().LegacyServiceRequestDetails.SingleAsync(x => x.TransactionId == transactionId);
        Assert.Equal("fees-v1", details.LegacySystem);
    }
    [Fact]
    public async Task Changed_legacy_retry_conflicts()
    {
        var transactionId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.ArchivedTransactions.Add(new ArchivedTransactionEntity { Id = Guid.NewGuid(), LegacySystem = "fees-v1", TransactionId = transactionId, TransactionType = "Fee" });
            await db.SaveChangesAsync();
        }
        var original = new { legacySystem = "fees-v1", transactionId, callBackUrl = "https://example.test/one", ccdCaseNumber = "123", fees = new[] { new { code = "FEE1", version = "1", calculatedAmount = 10m } } };
        var changed = new { legacySystem = "fees-v1", transactionId, callBackUrl = "https://example.test/two", ccdCaseNumber = "123", fees = new[] { new { code = "FEE1", version = "1", calculatedAmount = 10m } } };
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/legacy-service-request", original)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/legacy-service-request", changed)).StatusCode);
    }
    [Fact]
    public async Task Legacy_payment_is_imported_completed_and_is_idempotent()
    {
        var feeTransactionId = Guid.NewGuid().ToString();
        var paymentTransactionId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.ArchivedTransactions.AddRange(
                new ArchivedTransactionEntity { Id = Guid.NewGuid(), LegacySystem = "fees-v1", TransactionId = feeTransactionId, TransactionType = "Fee", FeeTotal = 10m },
                new ArchivedTransactionEntity { Id = Guid.NewGuid(), LegacySystem = "fees-v1", TransactionId = paymentTransactionId, TransactionType = "Payment", FeeTransactionId = feeTransactionId, LegacyPaymentReference = "LP-123", Amount = 10m, Currency = "GBP", ProviderTransactionId = "provider-456" });
            await db.SaveChangesAsync();
        }
        var legacySr = await client.PostAsJsonAsync("/legacy-service-request", new { legacySystem = "fees-v1", transactionId = feeTransactionId, callBackUrl = "https://example.test/callback", ccdCaseNumber = "123", fees = new[] { new { code = "FEE1", version = "1", calculatedAmount = 10m } } });
        var sr = (await legacySr.Content.ReadFromJsonAsync<LegacyServiceRequestResponse>())!.ServiceRequestReference;
        var body = new { legacySystem = "fees-v1", transactionId = paymentTransactionId, legacyPaymentReference = "LP-123", amount = 10m, currency = "GBP" };

        var first = await client.PostAsJsonAsync($"/service-request/{sr}/legacy-payments", body);
        var retry = await client.PostAsJsonAsync($"/service-request/{sr}/legacy-payments", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var response = await first.Content.ReadFromJsonAsync<LegacyPaymentResponse>();
        Assert.Equal(response!.PaymentReference, (await retry.Content.ReadFromJsonAsync<LegacyPaymentResponse>())!.PaymentReference);
        Assert.Equal("Success", response.Status);
        using var checkScope = factory.Services.CreateScope();
        var saved = await checkScope.ServiceProvider.GetRequiredService<PaymentDbContext>().Payments
            .Include(x => x.History).Include(x => x.LegacyDetails).SingleAsync(x => x.Reference == response.PaymentReference);
        Assert.Equal("Success", Assert.Single(saved.History).Status);
        Assert.Equal("provider-456", saved.LegacyDetails!.ProviderTransactionId);
        Assert.Equal(string.Empty, saved.ReturnUrl);
    }

    [Fact]
    public async Task Legacy_payment_rejects_archive_discrepancies()
    {
        var feeTransactionId = Guid.NewGuid().ToString();
        var paymentTransactionId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            db.ArchivedTransactions.AddRange(
                new ArchivedTransactionEntity { Id = Guid.NewGuid(), LegacySystem = "fees-v1", TransactionId = feeTransactionId, TransactionType = "Fee" },
                new ArchivedTransactionEntity { Id = Guid.NewGuid(), LegacySystem = "fees-v1", TransactionId = paymentTransactionId, TransactionType = "Payment", FeeTransactionId = feeTransactionId, LegacyPaymentReference = "LP-789", Amount = 10m, Currency = "GBP" });
            await db.SaveChangesAsync();
        }
        var legacySr = await client.PostAsJsonAsync("/legacy-service-request", new { legacySystem = "fees-v1", transactionId = feeTransactionId, callBackUrl = "https://example.test/callback", ccdCaseNumber = "123", fees = new[] { new { code = "FEE1", version = "1", calculatedAmount = 10m } } });
        var sr = (await legacySr.Content.ReadFromJsonAsync<LegacyServiceRequestResponse>())!.ServiceRequestReference;

        var result = await client.PostAsJsonAsync($"/service-request/{sr}/legacy-payments", new { legacySystem = "fees-v1", transactionId = paymentTransactionId, legacyPaymentReference = "LP-789", amount = 11m, currency = "GBP" });

        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
    }
    [Fact] public async Task Invalid_service_request_is_rejected() { var r = await client.PostAsJsonAsync("/service-request", new { callBackUrl = "x", ccdCaseNumber = "", fees = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task Service_request_requires_at_least_one_fee() { var r = await client.PostAsJsonAsync("/service-request", new { callBackUrl = "https://example.test/callback", ccdCaseNumber = "1234567890123456", fees = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task Service_request_rejects_a_zero_fee() { var r = await client.PostAsJsonAsync("/service-request", new { callBackUrl = "https://example.test/callback", ccdCaseNumber = "1234567890123456", fees = new[] { new { code = "FEE0001", version = "1", calculatedAmount = 0m } } }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task Malformed_service_request_json_is_rejected_before_the_action() { using var body = new StringContent("{ not-json", System.Text.Encoding.UTF8, "application/json"); var r = await client.PostAsync("/service-request", body); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType); }
    [Fact] public async Task Service_request_requires_json_content_type() { using var body = new StringContent("ccdCaseNumber=1234567890123456", System.Text.Encoding.UTF8, "text/plain"); var r = await client.PostAsync("/service-request", body); Assert.Equal(HttpStatusCode.UnsupportedMediaType, r.StatusCode); }
    [Fact] public async Task Missing_service_request_is_not_found() { var r = await client.PostAsJsonAsync("/service-request/nope/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return" }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
    [Fact] public async Task Card_payment_is_created() { var sr = await CreateSr(); var r = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return", language = "en" }); Assert.Equal(HttpStatusCode.Created, r.StatusCode); Assert.StartsWith("RC-", (await r.Content.ReadFromJsonAsync<CardPaymentResponse>())!.PaymentReference); }
    [Fact]
    public async Task Card_payment_does_not_modify_its_existing_service_request()
    {
        var sr = await CreateSr();

        using (var beforeScope = factory.Services.CreateScope())
        {
            var database = beforeScope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var serviceRequest = await database.ServiceRequests.AsNoTracking().SingleAsync(x => x.Reference == sr);
            serviceRequest.CallbackUrl = "https://stale.example.test/callback";
            database.ServiceRequests.Update(serviceRequest);
            await database.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new
        {
            currency = "GBP",
            amount = 10m,
            returnUrl = "https://example.test/return"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var afterScope = factory.Services.CreateScope();
        var saved = await afterScope.ServiceProvider.GetRequiredService<PaymentDbContext>()
            .ServiceRequests.AsNoTracking().SingleAsync(x => x.Reference == sr);
        Assert.Equal("https://stale.example.test/callback", saved.CallbackUrl);
    }
    [Fact] public async Task Wrong_amount_is_rejected() { var sr = await CreateSr(); var r = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "GBP", amount = 11m, returnUrl = "https://example.test/return" }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task Unsupported_currency_is_rejected() { var sr = await CreateSr(); var r = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "EUR", amount = 10m, returnUrl = "https://example.test/return" }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task Duplicate_active_payment_is_idempotent() { var sr = await CreateSr(); var body = new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return" }; var a = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", body); var b = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", body); Assert.Equal((await a.Content.ReadFromJsonAsync<CardPaymentResponse>())!.PaymentReference, (await b.Content.ReadFromJsonAsync<CardPaymentResponse>())!.PaymentReference); }
    [Fact] public async Task Payments_can_be_listed_and_retrieved()
    {
        var sr = await CreateSr();
        var created = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return" });
        var reference = (await created.Content.ReadFromJsonAsync<CardPaymentResponse>())!.PaymentReference;
        var list = await client.GetFromJsonAsync<List<PaymentReadResponse>>("/payments");
        var details = await client.GetFromJsonAsync<PaymentReadResponse>($"/payments/{reference}");
        Assert.Contains(list!, payment => payment.PaymentReference == reference);
        Assert.Equal(sr, details!.ServiceRequestReference);
        Assert.Equal(10m, details.Amount);
    }
    [Fact] public async Task Missing_payment_read_is_not_found() => Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/payments/does-not-exist")).StatusCode);
    [Fact] public async Task Payments_ui_supports_search_and_details()
    {
        var sr = await CreateSr();
        var created = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return" });
        var reference = (await created.Content.ReadFromJsonAsync<CardPaymentResponse>())!.PaymentReference;
        var list = await client.GetAsync($"/payments-ui?search={reference}&status=Initiated");
        var listHtml = await list.Content.ReadAsStringAsync();
        var details = await client.GetAsync($"/payments-ui/{reference}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Contains(reference, listHtml);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Contains("Payment details", await details.Content.ReadAsStringAsync());
    }
    [Fact] public async Task Missing_payment_ui_is_not_found() => Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/payments-ui/does-not-exist")).StatusCode);
    [Fact] public async Task Create_service_request_ui_posts_to_api()
    {
        var page = await client.GetAsync("/service-requests/new");
        var html = await page.Content.ReadAsStringAsync();
        var script = await client.GetStringAsync("/js/create-service-request.js");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Create a service request", html);
        Assert.Contains("ccdCaseNumber", html);
        Assert.Contains("fetch(\"/service-request\"", script);
    }
    [Fact] public async Task Success_persists_history_and_redirects() { var sr = await CreateSr(); var created = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return" }); var p = await created.Content.ReadFromJsonAsync<CardPaymentResponse>(); var result = await client.PostAsync($"/pay/{p!.PaymentReference}/success", null); Assert.Equal(HttpStatusCode.Redirect, result.StatusCode); using var scope = factory.Services.CreateScope(); var saved = await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Payments.Include(x => x.History).SingleAsync(x => x.Reference == p.PaymentReference); Assert.Equal("Success", saved.Status); Assert.Equal(2, saved.History.Count); }
}
