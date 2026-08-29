using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PaymentApi.Pages.Payments;

public sealed class IndexModel(PaymentQueryService payments, ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<PaymentReadResponse> Payments { get; private set; } = [];
    public bool LoadFailed { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public async Task OnGet(CancellationToken cancellationToken)
    {
        try
        {
            var results = await payments.GetAll(cancellationToken);
            Payments = results
                .Where(MatchesSearch)
                .Where(payment => string.IsNullOrWhiteSpace(Status) ||
                    payment.Status.Equals(Status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load payments for the UI");
            LoadFailed = true;
        }
    }

    private bool MatchesSearch(PaymentReadResponse payment)
    {
        if (string.IsNullOrWhiteSpace(Search)) return true;
        return new[] { payment.PaymentReference, payment.ServiceRequestReference,
                payment.CaseReference, payment.CcdCaseNumber }
            .Any(value => value?.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) is true);
    }
}
