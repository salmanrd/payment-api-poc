using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260902010000_AddLegacyPaymentImport")]
public partial class AddLegacyPaymentImport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("Amount", "ArchivedTransactions", "numeric(12,2)", nullable: true);
        migrationBuilder.AddColumn<string>("Currency", "ArchivedTransactions", "text", nullable: true);
        migrationBuilder.AddColumn<string>("FeeTransactionId", "ArchivedTransactions", "text", nullable: true);
        migrationBuilder.AddColumn<string>("LegacyPaymentReference", "ArchivedTransactions", "text", nullable: true);
        migrationBuilder.AddColumn<string>("ProviderTransactionId", "ArchivedTransactions", "text", nullable: true);

        migrationBuilder.CreateTable("LegacyPaymentDetails", table => new
        {
            Id = table.Column<Guid>("uuid"), PaymentEntityId = table.Column<Guid>("uuid"),
            LegacySystem = table.Column<string>("text"), TransactionId = table.Column<string>("text"),
            LegacyPaymentReference = table.Column<string>("text"), ProviderTransactionId = table.Column<string>("text", nullable: true),
            ImportedAt = table.Column<DateTimeOffset>("timestamp with time zone")
        }, constraints: table =>
        {
            table.PrimaryKey("PK_LegacyPaymentDetails", x => x.Id);
            table.ForeignKey("FK_LegacyPaymentDetails_Payments_PaymentEntityId", x => x.PaymentEntityId,
                "Payments", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_LegacyPaymentDetails_PaymentEntityId", "LegacyPaymentDetails", "PaymentEntityId", unique: true);
        migrationBuilder.CreateIndex("IX_LegacyPaymentDetails_LegacySystem_TransactionId", "LegacyPaymentDetails", new[] { "LegacySystem", "TransactionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LegacyPaymentDetails_LegacySystem_LegacyPaymentReference", "LegacyPaymentDetails", new[] { "LegacySystem", "LegacyPaymentReference" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("LegacyPaymentDetails");
        migrationBuilder.DropColumn("Amount", "ArchivedTransactions");
        migrationBuilder.DropColumn("Currency", "ArchivedTransactions");
        migrationBuilder.DropColumn("FeeTransactionId", "ArchivedTransactions");
        migrationBuilder.DropColumn("LegacyPaymentReference", "ArchivedTransactions");
        migrationBuilder.DropColumn("ProviderTransactionId", "ArchivedTransactions");
    }
}
