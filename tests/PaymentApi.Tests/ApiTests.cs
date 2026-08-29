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
    [Fact] public async Task Service_request_is_created() { var r = await client.PostAsJsonAsync("/service-request", Sr()); Assert.Equal(HttpStatusCode.Created, r.StatusCode); Assert.StartsWith("SR-", (await r.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference); }
    [Fact] public async Task Service_request_is_idempotent_by_ccd_case() { var ccd = Random.Shared.NextInt64().ToString(); var a = await client.PostAsJsonAsync("/service-request", Sr(ccd)); var b = await client.PostAsJsonAsync("/service-request", Sr(ccd)); Assert.Equal((await a.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference, (await b.Content.ReadFromJsonAsync<ServiceRequestResponse>())!.ServiceRequestReference); }
    [Fact] public async Task Invalid_service_request_is_rejected() { var r = await client.PostAsJsonAsync("/service-request", new { callBackUrl = "x", ccdCaseNumber = "", fees = Array.Empty<object>() }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
    [Fact] public async Task Missing_service_request_is_not_found() { var r = await client.PostAsJsonAsync("/service-request/nope/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return" }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
    [Fact] public async Task Card_payment_is_created() { var sr = await CreateSr(); var r = await client.PostAsJsonAsync($"/service-request/{sr}/card-payments", new { currency = "GBP", amount = 10m, returnUrl = "https://example.test/return", language = "en" }); Assert.Equal(HttpStatusCode.Created, r.StatusCode); Assert.StartsWith("RC-", (await r.Content.ReadFromJsonAsync<CardPaymentResponse>())!.PaymentReference); }
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
