using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260923000000_MakeLegacyPaymentTransactionIdOptional")]
public partial class MakeLegacyPaymentTransactionIdOptional : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "TransactionId",
            table: "LegacyPaymentDetails",
            type: "text",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "LegacyPaymentDetails"
            SET "TransactionId" = "Id"::text
            WHERE "TransactionId" IS NULL;
            """);
        migrationBuilder.AlterColumn<string>(
            name: "TransactionId",
            table: "LegacyPaymentDetails",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }
}
