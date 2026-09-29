using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PaymentApi.Migrations;

[DbContext(typeof(PaymentDbContext))]
[Migration("20260929010000_AddTransactionCcdCaseNumber")]
public partial class AddTransactionCcdCaseNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CCDCasenumber",
            table: "Transactions",
            type: "text",
            nullable: true);

        // Existing imports used CaseNo as the only available case identifier.
        migrationBuilder.Sql("UPDATE \"Transactions\" SET \"CCDCasenumber\" = \"CaseNo\"");

        migrationBuilder.AlterColumn<string>(
            name: "CCDCasenumber",
            table: "Transactions",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "text",
            oldNullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CCDCasenumber", table: "Transactions");
    }
}
