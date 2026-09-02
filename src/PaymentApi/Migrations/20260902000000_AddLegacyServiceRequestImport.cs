using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260902000000_AddLegacyServiceRequestImport")]
public partial class AddLegacyServiceRequestImport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_ServiceRequests_CcdCaseNumber", "ServiceRequests");
        migrationBuilder.CreateIndex("IX_ServiceRequests_CcdCaseNumber", "ServiceRequests", "CcdCaseNumber");
        migrationBuilder.CreateTable("ArchivedTransactions", table => new
        {
            Id = table.Column<Guid>("uuid"), LegacySystem = table.Column<string>("text"),
            TransactionId = table.Column<string>("text"), TransactionType = table.Column<string>("text"),
            CaseReference = table.Column<string>("text", nullable: true), CcdCaseNumber = table.Column<string>("text", nullable: true),
            FeeTotal = table.Column<decimal>("numeric(12,2)", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_ArchivedTransactions", x => x.Id));
        migrationBuilder.CreateTable("LegacyServiceRequestDetails", table => new
        {
            Id = table.Column<Guid>("uuid"), ServiceRequestEntityId = table.Column<Guid>("uuid"),
            LegacySystem = table.Column<string>("text"), TransactionId = table.Column<string>("text"),
            ImportedAt = table.Column<DateTimeOffset>("timestamp with time zone")
        }, constraints: table =>
        {
            table.PrimaryKey("PK_LegacyServiceRequestDetails", x => x.Id);
            table.ForeignKey("FK_LegacyServiceRequestDetails_ServiceRequests_ServiceRequestEntityId",
                x => x.ServiceRequestEntityId, "ServiceRequests", "Id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_ArchivedTransactions_LegacySystem_TransactionId", "ArchivedTransactions", new[] { "LegacySystem", "TransactionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LegacyServiceRequestDetails_LegacySystem_TransactionId", "LegacyServiceRequestDetails", new[] { "LegacySystem", "TransactionId" }, unique: true);
        migrationBuilder.CreateIndex("IX_LegacyServiceRequestDetails_ServiceRequestEntityId", "LegacyServiceRequestDetails", "ServiceRequestEntityId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ArchivedTransactions");
        migrationBuilder.DropTable("LegacyServiceRequestDetails");
        migrationBuilder.DropIndex("IX_ServiceRequests_CcdCaseNumber", "ServiceRequests");
        migrationBuilder.CreateIndex("IX_ServiceRequests_CcdCaseNumber", "ServiceRequests", "CcdCaseNumber", unique: true);
    }
}
