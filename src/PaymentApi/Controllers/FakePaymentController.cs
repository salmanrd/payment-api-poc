using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Controllers;

[ApiController]
[Route("pay/{paymentReference}")]
[Tags("Fake provider")]
public sealed class FakePaymentController(
    PaymentDbContext database,
    PaymentService payments) : ControllerBase
{
    [HttpGet]
    [Produces("text/html")]
    public async Task<IActionResult> Checkout(
        string paymentReference,
        CancellationToken cancellationToken)
    {
        var payment = await database.Payments.SingleOrDefaultAsync(
            item => item.Reference == paymentReference, cancellationToken);
        if (payment is null) return NotFound();

        var html = $"""
            <!doctype html><html><body><h1>Test payment {paymentReference}</h1>
            <p>Amount: {payment.Amount:0.00} {payment.Currency}</p>
            <form method="post" action="/pay/{paymentReference}/success"><button>Success</button></form>
            <form method="post" action="/pay/{paymentReference}/failure"><button>Failure</button></form>
            <form method="post" action="/pay/{paymentReference}/cancel"><button>Cancel</button></form>
            </body></html>
            """;
        return Content(html, "text/html");
    }

    [HttpPost("success")]
    public Task<IActionResult> Success(string paymentReference, CancellationToken cancellationToken) =>
        Transition(paymentReference, "Success", cancellationToken);

    [HttpPost("failure")]
    public Task<IActionResult> Failure(string paymentReference, CancellationToken cancellationToken) =>
        Transition(paymentReference, "Failed", cancellationToken);

    [HttpPost("cancel")]
    public Task<IActionResult> Cancel(string paymentReference, CancellationToken cancellationToken) =>
        Transition(paymentReference, "Cancelled", cancellationToken);

    private async Task<IActionResult> Transition(
        string paymentReference, string status, CancellationToken cancellationToken)
    {
        var payment = await payments.Transition(paymentReference, status, cancellationToken);
        return payment is null ? NotFound() : Redirect(payment.ReturnUrl);
    }
}
