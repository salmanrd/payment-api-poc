using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Pages.Cases;

public sealed class DetailsModel(PaymentDbContext database, ILogger<DetailsModel> logger) : PageModel
{
    public string CcdCaseNumber { get; private set; } = "";
    public IReadOnlyList<ServiceRequestRow> ServiceRequests { get; private set; } = [];
    public IReadOnlyList<PaymentRow> Payments { get; private set; } = [];
    public bool LoadFailed { get; private set; }

    public async Task OnGet(string ccdCaseNumber, CancellationToken cancellationToken)
    {
        CcdCaseNumber = ccdCaseNumber;

        try
        {
            var serviceRequests = await database.ServiceRequests
                .AsNoTracking()
                .Include(serviceRequest => serviceRequest.Fees)
                .Include(serviceRequest => serviceRequest.Payments)
                .Where(serviceRequest => serviceRequest.CcdCaseNumber == ccdCaseNumber)
                .OrderBy(serviceRequest => serviceRequest.Created)
                .ToListAsync(cancellationToken);

            ServiceRequests = serviceRequests.Select(serviceRequest =>
            {
                var amount = serviceRequest.Fees.Sum(fee => fee.Amount);
                var paid = serviceRequest.Payments
                    .Where(payment => payment.Status.Equals("Success", StringComparison.OrdinalIgnoreCase))
                    .Sum(payment => payment.Amount);
                var status = paid >= amount ? "Paid" : paid > 0 ? "Partially paid" : "Not paid";
                return new ServiceRequestRow(serviceRequest.Reference, status, amount, serviceRequest.CaseReference);
            }).ToList();

            Payments = serviceRequests
                .SelectMany(serviceRequest => serviceRequest.Payments.Select(payment => new PaymentRow(
                    payment.Reference,
                    payment.Status,
                    payment.Amount,
                    payment.Created,
                    serviceRequest.Reference)))
                .OrderBy(payment => payment.DateAllocated)
                .ToList();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load case {CcdCaseNumber}", ccdCaseNumber);
            LoadFailed = true;
        }
    }

    public sealed record ServiceRequestRow(string Reference, string Status, decimal Amount, string? Party);
    public sealed record PaymentRow(string Reference, string Status, decimal Amount, DateTimeOffset DateAllocated, string ServiceRequestReference);
}
