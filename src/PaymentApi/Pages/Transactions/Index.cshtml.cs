using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace PaymentApi.Pages.Transactions;

public sealed class IndexModel(PaymentDbContext database) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? CaseNo { get; set; }

    public IReadOnlyList<TransactionEntity> Transactions { get; private set; } = [];

    public async Task OnGet(CancellationToken cancellationToken)
    {
        var transactions = database.Transactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(CaseNo))
        {
            var term = CaseNo.Trim().ToLower();
            transactions = transactions.Where(transaction => transaction.CaseNo.ToLower().Contains(term));
        }

        Transactions = await transactions
            .OrderByDescending(transaction => transaction.TransactionDate)
            .ToListAsync(cancellationToken);
    }
}
