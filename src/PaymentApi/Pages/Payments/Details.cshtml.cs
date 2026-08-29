using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PaymentApi.Pages.Payments;

public sealed class DetailsModel(PaymentQueryService payments, ILogger<DetailsModel> logger) : PageModel
{
    public PaymentReadResponse? Payment { get; private set; }
    public bool LoadFailed { get; private set; }

    public async Task OnGet(string paymentReference, CancellationToken cancellationToken)
    {
        try
        {
            Payment = await payments.Get(paymentReference, cancellationToken);
            if (Payment is null) Response.StatusCode = StatusCodes.Status404NotFound;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to load payment {PaymentReference} for the UI", paymentReference);
            LoadFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
    }
}
