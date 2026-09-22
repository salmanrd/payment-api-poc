using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Pages.ServiceRequests;

public sealed class DetailsModel(PaymentDbContext database, ILogger<DetailsModel> logger) : PageModel
{
    public ServiceRequestDetails? ServiceRequest { get; private set; }
    public bool LoadFailed { get; private set; }

    public async Task OnGet(string serviceRequestReference, CancellationToken cancellationToken)
    {
        try
        {
            var serviceRequest = await database.ServiceRequests
                .AsNoTracking()
                .Include(request => request.Fees)
                .Include(request => request.Payments)
                .SingleOrDefaultAsync(request => request.Reference == serviceRequestReference, cancellationToken);

            if (serviceRequest is null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var totalFees = serviceRequest.Fees.Sum(fee => fee.Amount);
            var payments = serviceRequest.Payments
                .OrderBy(payment => payment.Created)
                .Select(payment => new PaymentDetails(payment.Reference, payment.Created, payment.Amount, payment.Status))
                .ToList();
            var successfulPayments = payments
                .Where(payment => payment.Status.Equals("Success", StringComparison.OrdinalIgnoreCase))
                .Sum(payment => payment.Amount);

            ServiceRequest = new ServiceRequestDetails(
                serviceRequest.Reference,
                successfulPayments >= totalFees ? "Paid" : successfulPayments > 0 ? "Partially paid" : "Not paid",
                serviceRequest.Created,
                serviceRequest.CaseReference,
                serviceRequest.CcdCaseNumber,
                serviceRequest.Fees.Select(fee => new FeeDetails(fee.Code, fee.Version, fee.Amount)).ToList(),
                payments,
                totalFees,
                successfulPayments);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load service request {ServiceRequestReference}", serviceRequestReference);
            LoadFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }

    public sealed record ServiceRequestDetails(
        string Reference,
        string Status,
        DateTimeOffset Created,
        string? CaseReference,
        string CcdCaseNumber,
        IReadOnlyList<FeeDetails> Fees,
        IReadOnlyList<PaymentDetails> Payments,
        decimal TotalFees,
        decimal TotalPaid);

    public sealed record FeeDetails(string Code, string Version, decimal Amount);
    public sealed record PaymentDetails(string Reference, DateTimeOffset Created, decimal Amount, string Status);
}
