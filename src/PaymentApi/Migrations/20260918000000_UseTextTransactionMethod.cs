using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260918000000_UseTextTransactionMethod")]
public partial class UseTextTransactionMethod : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Transactions" RENAME COLUMN "TransactionMethodId" TO "TransactionMethod";
            ALTER TABLE "Transactions" ALTER COLUMN "TransactionMethod" TYPE text USING "TransactionMethod"::text;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Transactions" ALTER COLUMN "TransactionMethod" TYPE integer USING "TransactionMethod"::integer;
            ALTER TABLE "Transactions" RENAME COLUMN "TransactionMethod" TO "TransactionMethodId";
            """);
    }
}
