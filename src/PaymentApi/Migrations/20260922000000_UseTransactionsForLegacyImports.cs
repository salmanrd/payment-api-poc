using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260922000000_UseTransactionsForLegacyImports")]
public partial class UseTransactionsForLegacyImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CcdCaseNumber",
            table: "LegacyServiceRequestDetails",
            type: "text",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE "LegacyServiceRequestDetails" AS details
            SET "CcdCaseNumber" = requests."CcdCaseNumber"
            FROM "ServiceRequests" AS requests
            WHERE details."ServiceRequestEntityId" = requests."Id";
            """);

        migrationBuilder.AlterColumn<string>(
            name: "CcdCaseNumber",
            table: "LegacyServiceRequestDetails",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "IX_LegacyServiceRequestDetails_LegacySystem_TransactionId",
            table: "LegacyServiceRequestDetails");
        migrationBuilder.DropColumn(
            name: "TransactionId",
            table: "LegacyServiceRequestDetails");
        migrationBuilder.CreateIndex(
            name: "IX_LegacyServiceRequestDetails_LegacySystem_CcdCaseNumber",
            table: "LegacyServiceRequestDetails",
            columns: new[] { "LegacySystem", "CcdCaseNumber" },
            unique: true);

        migrationBuilder.DropTable(name: "ArchivedTransactions");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArchivedTransactions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                LegacySystem = table.Column<string>(type: "text", nullable: false),
                TransactionId = table.Column<string>(type: "text", nullable: false),
                TransactionType = table.Column<string>(type: "text", nullable: false),
                CaseReference = table.Column<string>(type: "text", nullable: true),
                CcdCaseNumber = table.Column<string>(type: "text", nullable: true),
                FeeTotal = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                FeeTransactionId = table.Column<string>(type: "text", nullable: true),
                LegacyPaymentReference = table.Column<string>(type: "text", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                Currency = table.Column<string>(type: "text", nullable: true),
                ProviderTransactionId = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ArchivedTransactions", x => x.Id));
        migrationBuilder.CreateIndex(
            name: "IX_ArchivedTransactions_LegacySystem_TransactionId",
            table: "ArchivedTransactions",
            columns: new[] { "LegacySystem", "TransactionId" },
            unique: true);

        migrationBuilder.DropIndex(
            name: "IX_LegacyServiceRequestDetails_LegacySystem_CcdCaseNumber",
            table: "LegacyServiceRequestDetails");
        migrationBuilder.AddColumn<string>(
            name: "TransactionId",
            table: "LegacyServiceRequestDetails",
            type: "text",
            nullable: false,
            defaultValue: "");
        migrationBuilder.Sql("""
            UPDATE "LegacyServiceRequestDetails"
            SET "TransactionId" = "CcdCaseNumber";
            """);
        migrationBuilder.DropColumn(
            name: "CcdCaseNumber",
            table: "LegacyServiceRequestDetails");
        migrationBuilder.CreateIndex(
            name: "IX_LegacyServiceRequestDetails_LegacySystem_TransactionId",
            table: "LegacyServiceRequestDetails",
            columns: new[] { "LegacySystem", "TransactionId" },
            unique: true);
    }
}
