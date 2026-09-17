using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260917000000_UseNumericTransactionIds")]
public partial class UseNumericTransactionIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL cannot cast UUIDs directly to bigint. Hash any existing UUID
        // values so their rows remain addressable while changing the key type.
        migrationBuilder.Sql("""
            ALTER TABLE "Transactions" DROP CONSTRAINT "PK_Transactions";
            ALTER TABLE "Transactions" ADD COLUMN "NumericTransactionId" bigint;
            UPDATE "Transactions"
            SET "NumericTransactionId" = ('x' || substr(md5("TransactionId"::text), 1, 16))::bit(64)::bigint;
            ALTER TABLE "Transactions" DROP COLUMN "TransactionId";
            ALTER TABLE "Transactions" RENAME COLUMN "NumericTransactionId" TO "TransactionId";
            ALTER TABLE "Transactions" ALTER COLUMN "TransactionId" SET NOT NULL;
            ALTER TABLE "Transactions" ADD CONSTRAINT "PK_Transactions" PRIMARY KEY ("TransactionId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Transactions" DROP CONSTRAINT "PK_Transactions";
            ALTER TABLE "Transactions" ADD COLUMN "UuidTransactionId" uuid;
            UPDATE "Transactions"
            SET "UuidTransactionId" = md5("TransactionId"::text)::uuid;
            ALTER TABLE "Transactions" DROP COLUMN "TransactionId";
            ALTER TABLE "Transactions" RENAME COLUMN "UuidTransactionId" TO "TransactionId";
            ALTER TABLE "Transactions" ALTER COLUMN "TransactionId" SET NOT NULL;
            ALTER TABLE "Transactions" ADD CONSTRAINT "PK_Transactions" PRIMARY KEY ("TransactionId");
            """);
    }
}
