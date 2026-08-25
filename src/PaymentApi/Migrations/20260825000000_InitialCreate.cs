using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260825000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("ServiceRequests", table => new { Id = table.Column<Guid>("uuid"), Reference = table.Column<string>("text"), CallbackUrl = table.Column<string>("text"), CaseReference = table.Column<string>("text", nullable: true), CcdCaseNumber = table.Column<string>("text"), Created = table.Column<DateTimeOffset>("timestamp with time zone") }, constraints: table => table.PrimaryKey("PK_ServiceRequests", x => x.Id));
        migrationBuilder.CreateTable("Fees", table => new { Id = table.Column<Guid>("uuid"), ServiceRequestEntityId = table.Column<Guid>("uuid"), Code = table.Column<string>("text"), Version = table.Column<string>("text"), Amount = table.Column<decimal>("numeric(12,2)") }, constraints: table => { table.PrimaryKey("PK_Fees", x => x.Id); table.ForeignKey("FK_Fees_ServiceRequests_ServiceRequestEntityId", x => x.ServiceRequestEntityId, "ServiceRequests", "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateTable("Payments", table => new { Id = table.Column<Guid>("uuid"), ServiceRequestEntityId = table.Column<Guid>("uuid"), Reference = table.Column<string>("text"), Amount = table.Column<decimal>("numeric(12,2)"), Currency = table.Column<string>("text"), ReturnUrl = table.Column<string>("text"), Status = table.Column<string>("text"), Created = table.Column<DateTimeOffset>("timestamp with time zone") }, constraints: table => { table.PrimaryKey("PK_Payments", x => x.Id); table.ForeignKey("FK_Payments_ServiceRequests_ServiceRequestEntityId", x => x.ServiceRequestEntityId, "ServiceRequests", "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateTable("StatusHistory", table => new { Id = table.Column<Guid>("uuid"), PaymentEntityId = table.Column<Guid>("uuid"), Status = table.Column<string>("text"), Created = table.Column<DateTimeOffset>("timestamp with time zone") }, constraints: table => { table.PrimaryKey("PK_StatusHistory", x => x.Id); table.ForeignKey("FK_StatusHistory_Payments_PaymentEntityId", x => x.PaymentEntityId, "Payments", "Id", onDelete: ReferentialAction.Cascade); });
        migrationBuilder.CreateIndex("IX_ServiceRequests_Reference", "ServiceRequests", "Reference", unique: true); migrationBuilder.CreateIndex("IX_ServiceRequests_CcdCaseNumber", "ServiceRequests", "CcdCaseNumber", unique: true);
        migrationBuilder.CreateIndex("IX_Fees_ServiceRequestEntityId", "Fees", "ServiceRequestEntityId"); migrationBuilder.CreateIndex("IX_Payments_ServiceRequestEntityId", "Payments", "ServiceRequestEntityId"); migrationBuilder.CreateIndex("IX_Payments_Reference", "Payments", "Reference", unique: true); migrationBuilder.CreateIndex("IX_StatusHistory_PaymentEntityId", "StatusHistory", "PaymentEntityId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) { migrationBuilder.DropTable("Fees"); migrationBuilder.DropTable("StatusHistory"); migrationBuilder.DropTable("Payments"); migrationBuilder.DropTable("ServiceRequests"); }
}
