using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi;

public interface IPaymentProvider { string GetCheckoutUrl(string paymentReference); }

public sealed class PaymentQueryService(PaymentDbContext database)
{
    public async Task<IReadOnlyList<PaymentReadResponse>> GetAll(CancellationToken cancellationToken) =>
        await database.Payments.AsNoTracking()
            .OrderByDescending(payment => payment.Created)
            .Select(payment => new PaymentReadResponse(
                payment.Reference,
                payment.ServiceRequest.Reference,
                payment.ServiceRequest.CaseReference,
                payment.ServiceRequest.CcdCaseNumber,
                payment.Amount,
                payment.Currency,
                payment.Status,
                payment.Created))
            .ToListAsync(cancellationToken);

    public Task<PaymentReadResponse?> Get(string reference, CancellationToken cancellationToken) =>
        database.Payments.AsNoTracking()
            .Where(payment => payment.Reference == reference)
            .Select(payment => new PaymentReadResponse(
                payment.Reference,
                payment.ServiceRequest.Reference,
                payment.ServiceRequest.CaseReference,
                payment.ServiceRequest.CcdCaseNumber,
                payment.Amount,
                payment.Currency,
                payment.Status,
                payment.Created))
            .SingleOrDefaultAsync(cancellationToken);
}

public sealed class FakePaymentProvider(IConfiguration configuration) : IPaymentProvider
{
    public string GetCheckoutUrl(string reference) => $"{configuration["PublicBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080"}/pay/{reference}";
}

public sealed class PaymentService(PaymentDbContext db, IPaymentProvider provider, IHttpClientFactory clients, ILogger<PaymentService> logger)
{
    public async Task<ServiceRequestEntity> CreateServiceRequest(CreateServiceRequest request, CancellationToken ct)
    {
        var existing = await db.ServiceRequests.Include(x => x.Fees).SingleOrDefaultAsync(x => x.CcdCaseNumber == request.CcdCaseNumber, ct);
        if (existing is not null) return existing;
        var now = DateTimeOffset.UtcNow;
        var entity = new ServiceRequestEntity { Id = Guid.NewGuid(), Reference = Reference("SR", now), CallbackUrl = request.CallBackUrl, CaseReference = request.CaseReference, CcdCaseNumber = request.CcdCaseNumber, Created = now,
            Fees = request.Fees.Select(x => new FeeEntity { Id = Guid.NewGuid(), Code = x.Code, Version = x.Version, Amount = x.CalculatedAmount }).ToList() };
        db.Add(entity); await db.SaveChangesAsync(ct); return entity;
    }
    public async Task<(PaymentEntity? Payment, string? Error)> CreatePayment(string serviceReference, CreateCardPayment request, CancellationToken ct)
    {
        var sr = await db.ServiceRequests.Include(x => x.Fees).Include(x => x.Payments).SingleOrDefaultAsync(x => x.Reference == serviceReference, ct);
        if (sr is null) return (null, "Service request not found");
        if (sr.Fees.Sum(x => x.Amount) != request.Amount) return (null, "Payment amount must equal the service request fee total");
        var existing = sr.Payments.FirstOrDefault(x => x.Amount == request.Amount && x.Currency == request.Currency && x.Status is "Initiated" or "Success");
        if (existing is not null) return (existing, null);
        var now = DateTimeOffset.UtcNow;
        var payment = new PaymentEntity { Id = Guid.NewGuid(), Reference = Reference("RC", now), Amount = request.Amount, Currency = request.Currency, ReturnUrl = request.ReturnUrl, Created = now, History = [new() { Id = Guid.NewGuid(), Status = "Initiated", Created = now }] };
        sr.Payments.Add(payment); await db.SaveChangesAsync(ct); return (payment, null);
    }
    public CardPaymentResponse Response(PaymentEntity p) => new(p.Reference, p.Status, provider.GetCheckoutUrl(p.Reference));
    public async Task<PaymentEntity?> Transition(string reference, string target, CancellationToken ct)
    {
        var p = await db.Payments.Include(x => x.ServiceRequest).Include(x => x.History).SingleOrDefaultAsync(x => x.Reference == reference, ct);
        if (p is null || p.Status != "Initiated") return p;
        p.Status = target; p.History.Add(new() { Id = Guid.NewGuid(), Status = target, Created = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct); // State is durable before the best-effort callback.
        try { await clients.CreateClient("callbacks").PostAsJsonAsync(p.ServiceRequest.CallbackUrl, new { paymentReference = p.Reference, status = p.Status }, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Callback delivery failed for {Reference}", p.Reference); }
        return p;
    }
    private static string Reference(string prefix, DateTimeOffset now) => $"{prefix}-{now:yyyyMMdd}-{Random.Shared.NextInt64(0, 10_000_000_000):D10}";
}
