using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
        var now = DateTimeOffset.UtcNow;
        var entity = new ServiceRequestEntity { Id = Guid.NewGuid(), Reference = Reference("SR", now), CallbackUrl = request.CallBackUrl, CaseReference = request.CaseReference, CcdCaseNumber = request.CcdCaseNumber, Created = now,
            Fees = request.Fees.Select(x => new FeeEntity { Id = Guid.NewGuid(), Code = x.Code, Version = x.Version, Amount = x.CalculatedAmount }).ToList() };
        db.Add(entity); await db.SaveChangesAsync(ct); return entity;
    }
    public async Task<LegacyImportResult> CreateLegacyServiceRequest(CreateLegacyServiceRequest request, CancellationToken ct)
    {
        var existing = await db.LegacyServiceRequestDetails.AsNoTracking()
            .Include(x => x.ServiceRequest).ThenInclude(x => x.Fees)
            .SingleOrDefaultAsync(x => x.LegacySystem == request.LegacySystem && x.CcdCaseNumber == request.CcdCaseNumber, ct);
        if (existing is not null)
            return Matches(existing.ServiceRequest, request)
                ? new(existing.ServiceRequest, null, false)
                : new(null, "A materially different service request has already been imported for this legacy case", false);

        if (!await db.Transactions.AsNoTracking().AnyAsync(x => x.CaseNo == request.CcdCaseNumber, ct))
            return new(null, "Case number not found in transactions", false);

        IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var entity = new ServiceRequestEntity
            {
                Id = Guid.NewGuid(), Reference = Reference("SR", now), CallbackUrl = request.CallBackUrl,
                CaseReference = request.CaseReference, CcdCaseNumber = request.CcdCaseNumber, Created = now,
                Fees = request.Fees.Select(x => new FeeEntity { Id = Guid.NewGuid(), Code = x.Code, Version = x.Version, Amount = x.CalculatedAmount }).ToList(),
                LegacyDetails = new LegacyServiceRequestDetailsEntity
                {
                    Id = Guid.NewGuid(), LegacySystem = request.LegacySystem,
                    CcdCaseNumber = request.CcdCaseNumber, ImportedAt = now
                }
            };
            db.ServiceRequests.Add(entity);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(entity, null, true);
        }
        catch (DbUpdateException) when (transaction is not null)
        {
            // The database uniqueness constraint arbitrates simultaneous imports.
            // Read the winner after rolling back this insert and apply normal retry semantics.
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            transaction = null;
            db.ChangeTracker.Clear();
            var winner = await db.LegacyServiceRequestDetails.AsNoTracking()
                .Include(x => x.ServiceRequest).ThenInclude(x => x.Fees)
                .SingleOrDefaultAsync(x => x.LegacySystem == request.LegacySystem && x.CcdCaseNumber == request.CcdCaseNumber, ct);
            if (winner is null) throw;
            return Matches(winner.ServiceRequest, request)
                ? new(winner.ServiceRequest, null, false)
                : new(null, "A materially different service request has already been imported for this legacy case", false);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static bool Matches(ServiceRequestEntity existing, CreateLegacyServiceRequest request) =>
        existing.CcdCaseNumber == request.CcdCaseNumber &&
        existing.CaseReference == request.CaseReference &&
        existing.CallbackUrl == request.CallBackUrl &&
        existing.Fees.OrderBy(x => x.Code).ThenBy(x => x.Version).ThenBy(x => x.Amount)
            .Select(x => (x.Code, x.Version, x.Amount))
            .SequenceEqual(request.Fees.OrderBy(x => x.Code).ThenBy(x => x.Version).ThenBy(x => x.CalculatedAmount)
                .Select(x => (x.Code, x.Version, x.CalculatedAmount)));

    public async Task<LegacyPaymentImportResult> CreateLegacyPayment(
        string serviceReference, CreateLegacyPayment request, CancellationToken ct)
    {
        var existing = await db.LegacyPaymentDetails.AsNoTracking().Include(x => x.Payment).ThenInclude(x => x.ServiceRequest)
            .SingleOrDefaultAsync(x => x.LegacySystem == request.LegacySystem && x.TransactionId == request.TransactionId, ct);
        if (existing is not null)
            return LegacyPaymentMatches(existing, serviceReference, request)
                ? new(existing.Payment, null, false, false)
                : new(null, "This legacy payment transaction has already been imported with incompatible values", false, true);

        var referenceOwner = await db.LegacyPaymentDetails.AsNoTracking()
            .AnyAsync(x => x.LegacySystem == request.LegacySystem &&
                x.LegacyPaymentReference == request.LegacyPaymentReference, ct);
        if (referenceOwner)
            return new(null, "This legacy payment reference has already been imported", false, true);

        var serviceRequest = await db.ServiceRequests.AsNoTracking().Include(x => x.LegacyDetails)
            .SingleOrDefaultAsync(x => x.Reference == serviceReference, ct);
        if (serviceRequest is null) return new(null, "Service request not found", false, false);
        if (serviceRequest.LegacyDetails is null)
            return new(null, "Service request does not have legacy provenance", false, false);

        if (!long.TryParse(request.TransactionId, out var transactionId))
            return new(null, "Transaction not found", false, false);
        var sourceTransaction = await db.Transactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TransactionId == transactionId, ct);
        if (sourceTransaction is null) return new(null, "Transaction not found", false, false);
        if (!string.Equals(sourceTransaction.TransactionType, "Payment", StringComparison.OrdinalIgnoreCase))
            return new(null, "Transaction must have transaction type Payment", false, true);
        if (serviceRequest.LegacyDetails.LegacySystem != request.LegacySystem ||
            sourceTransaction.CaseNo != serviceRequest.CcdCaseNumber)
            return new(null, "Transaction does not belong to the service request's case", false, true);
        if (sourceTransaction.PaymentReference != request.LegacyPaymentReference || sourceTransaction.Amount != request.Amount)
            return new(null, "Legacy payment reference or amount does not match the transaction", false, true);

        IDbContextTransaction? transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var payment = new PaymentEntity
            {
                Id = Guid.NewGuid(), ServiceRequestEntityId = serviceRequest.Id, Reference = Reference("RC", now),
                Amount = request.Amount, Currency = request.Currency, ReturnUrl = "", Status = "Success", Created = now,
                History = [new() { Id = Guid.NewGuid(), Status = "Success", Created = now }],
                LegacyDetails = new LegacyPaymentDetailsEntity
                {
                    Id = Guid.NewGuid(), LegacySystem = request.LegacySystem, TransactionId = request.TransactionId,
                    LegacyPaymentReference = request.LegacyPaymentReference,
                    ImportedAt = now
                }
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return new(payment, null, true, false);
        }
        catch (DbUpdateException) when (transaction is not null)
        {
            await transaction.RollbackAsync(ct);
            await transaction.DisposeAsync();
            transaction = null;
            db.ChangeTracker.Clear();
            var winner = await db.LegacyPaymentDetails.AsNoTracking().Include(x => x.Payment).ThenInclude(x => x.ServiceRequest)
                .SingleOrDefaultAsync(x => x.LegacySystem == request.LegacySystem &&
                    (x.TransactionId == request.TransactionId || x.LegacyPaymentReference == request.LegacyPaymentReference), ct);
            if (winner is null) throw;
            return LegacyPaymentMatches(winner, serviceReference, request)
                ? new(winner.Payment, null, false, false)
                : new(null, "This legacy payment transaction has already been imported with incompatible values", false, true);
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    private static bool LegacyPaymentMatches(LegacyPaymentDetailsEntity existing, string serviceReference, CreateLegacyPayment request) =>
        existing.Payment.ServiceRequestEntityId != Guid.Empty &&
        existing.Payment.ServiceRequest.Reference == serviceReference &&
        existing.LegacyPaymentReference == request.LegacyPaymentReference &&
        existing.Payment.Amount == request.Amount && existing.Payment.Currency == request.Currency;
    public async Task<(PaymentEntity? Payment, string? Error)> CreatePayment(string serviceReference, CreateCardPayment request, CancellationToken ct)
    {
        // The service request is only used to validate and associate the payment. Keeping
        // the existing aggregate out of the change tracker ensures SaveChanges only
        // inserts the new payment and its history; it must never try to update an existing
        // service request (which can surface as a false optimistic-concurrency failure).
        var sr = await db.ServiceRequests
            .AsNoTracking()
            .Include(x => x.Fees)
            .SingleOrDefaultAsync(x => x.Reference == serviceReference, ct);
        if (sr is null) return (null, "Service request not found");
        if (sr.Fees.Sum(x => x.Amount) != request.Amount) return (null, "Payment amount must equal the service request fee total");
        var existing = await db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.ServiceRequestEntityId == sr.Id &&
                x.Amount == request.Amount &&
                x.Currency == request.Currency &&
                (x.Status == "Initiated" || x.Status == "Success"), ct);
        if (existing is not null) return (existing, null);
        var now = DateTimeOffset.UtcNow;
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            ServiceRequestEntityId = sr.Id,
            Reference = Reference("RC", now),
            Amount = request.Amount,
            Currency = request.Currency,
            ReturnUrl = request.ReturnUrl,
            Created = now,
            History = [new() { Id = Guid.NewGuid(), Status = "Initiated", Created = now }]
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return (payment, null);
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

public sealed record LegacyImportResult(ServiceRequestEntity? ServiceRequest, string? Error, bool Created);
public sealed record LegacyPaymentImportResult(PaymentEntity? Payment, string? Error, bool Created, bool Conflict);
