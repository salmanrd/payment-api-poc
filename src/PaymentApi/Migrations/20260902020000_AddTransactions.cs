using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260902020000_AddTransactions")]
public partial class AddTransactions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Transactions",
            columns: table => new
            {
                TransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                CaseNo = table.Column<string>(type: "text", nullable: false),
                TransactionType = table.Column<string>(type: "text", nullable: false),
                TransactionMethodId = table.Column<int>(type: "integer", nullable: false),
                TransactionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                TransactionStatus = table.Column<string>(type: "text", nullable: false),
                OriginalPaymentReference = table.Column<string>(type: "text", nullable: true),
                PaymentReference = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Transactions", x => x.TransactionId);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Transactions");
    }
}
