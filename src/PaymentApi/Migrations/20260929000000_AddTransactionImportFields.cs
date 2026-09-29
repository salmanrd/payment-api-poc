using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260929000000_AddTransactionImportFields")]
public partial class AddTransactionImportFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "AggregatedPaymentURN", table: "Transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<string>(name: "BarclaycardTransactionId", table: "Transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ClearedDate", table: "Transactions", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "ExpectedDate", table: "Transactions", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Last4DigitsCard", table: "Transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "LiberataNotifiedAggregatedPaymentDate", table: "Transactions", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(name: "LiberataNotifiedDate", table: "Transactions", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<string>(name: "Notes", table: "Transactions", type: "text", nullable: true);
        migrationBuilder.AddColumn<long>(name: "ReferringTransactionId", table: "Transactions", type: "bigint", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AggregatedPaymentURN", table: "Transactions");
        migrationBuilder.DropColumn(name: "BarclaycardTransactionId", table: "Transactions");
        migrationBuilder.DropColumn(name: "ClearedDate", table: "Transactions");
        migrationBuilder.DropColumn(name: "ExpectedDate", table: "Transactions");
        migrationBuilder.DropColumn(name: "Last4DigitsCard", table: "Transactions");
        migrationBuilder.DropColumn(name: "LiberataNotifiedAggregatedPaymentDate", table: "Transactions");
        migrationBuilder.DropColumn(name: "LiberataNotifiedDate", table: "Transactions");
        migrationBuilder.DropColumn(name: "Notes", table: "Transactions");
        migrationBuilder.DropColumn(name: "ReferringTransactionId", table: "Transactions");
    }
}
