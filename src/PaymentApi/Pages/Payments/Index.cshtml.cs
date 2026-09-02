using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Pages.Payments;

public sealed class IndexModel(PaymentDbContext database, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public bool SearchFailed { get; private set; }
    public bool NoCaseFound { get; private set; }

    public async Task<IActionResult> OnGet(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Search)) return Page();

        try
        {
            var term = Search.Trim();
            var serviceRequests = await database.ServiceRequests
                .AsNoTracking()
                .Include(serviceRequest => serviceRequest.Payments)
                .ToListAsync(cancellationToken);
            var ccdCaseNumber = serviceRequests.FirstOrDefault(serviceRequest =>
                Matches(serviceRequest.Reference, term) ||
                Matches(serviceRequest.CaseReference, term) ||
                Matches(serviceRequest.CcdCaseNumber, term) ||
                serviceRequest.Payments.Any(payment => Matches(payment.Reference, term)))?.CcdCaseNumber;

            if (ccdCaseNumber is null)
            {
                NoCaseFound = true;
                return Page();
            }

            return RedirectToPage("/Cases/Details", new { ccdCaseNumber });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to search for case {Search}", Search);
            SearchFailed = true;
            return Page();
        }
    }

    private static bool Matches(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) is true;
}
