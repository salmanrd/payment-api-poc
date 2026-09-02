using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable
namespace PaymentApi.Migrations;
[DbContext(typeof(PaymentDbContext))]
partial class PaymentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder b)
    {
        b.HasAnnotation("ProductVersion", "8.0.8").HasAnnotation("Relational:MaxIdentifierLength", 63);
        b.Entity("PaymentApi.ServiceRequestEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<string>("Reference").IsRequired(); e.Property<string>("CallbackUrl").IsRequired(); e.Property<string>("CaseReference"); e.Property<string>("CcdCaseNumber").IsRequired(); e.Property<DateTimeOffset>("Created"); e.HasKey("Id"); e.HasIndex("Reference").IsUnique(); e.HasIndex("CcdCaseNumber"); e.ToTable("ServiceRequests"); });
        b.Entity("PaymentApi.ArchivedTransactionEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<string>("LegacySystem").IsRequired(); e.Property<string>("TransactionId").IsRequired(); e.Property<string>("TransactionType").IsRequired(); e.Property<string>("CaseReference"); e.Property<string>("CcdCaseNumber"); e.Property<decimal?>("FeeTotal").HasPrecision(12,2); e.Property<string>("FeeTransactionId"); e.Property<string>("LegacyPaymentReference"); e.Property<decimal?>("Amount").HasPrecision(12,2); e.Property<string>("Currency"); e.Property<string>("ProviderTransactionId"); e.HasKey("Id"); e.HasIndex("LegacySystem", "TransactionId").IsUnique(); e.ToTable("ArchivedTransactions"); });
        b.Entity("PaymentApi.LegacyServiceRequestDetailsEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<Guid>("ServiceRequestEntityId"); e.Property<string>("LegacySystem").IsRequired(); e.Property<string>("TransactionId").IsRequired(); e.Property<DateTimeOffset>("ImportedAt"); e.HasKey("Id"); e.HasIndex("LegacySystem", "TransactionId").IsUnique(); e.HasIndex("ServiceRequestEntityId").IsUnique(); e.ToTable("LegacyServiceRequestDetails"); });
        b.Entity("PaymentApi.LegacyPaymentDetailsEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<Guid>("PaymentEntityId"); e.Property<string>("LegacySystem").IsRequired(); e.Property<string>("TransactionId").IsRequired(); e.Property<string>("LegacyPaymentReference").IsRequired(); e.Property<string>("ProviderTransactionId"); e.Property<DateTimeOffset>("ImportedAt"); e.HasKey("Id"); e.HasIndex("PaymentEntityId").IsUnique(); e.HasIndex("LegacySystem", "TransactionId").IsUnique(); e.HasIndex("LegacySystem", "LegacyPaymentReference").IsUnique(); e.ToTable("LegacyPaymentDetails"); });
        b.Entity("PaymentApi.FeeEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<Guid>("ServiceRequestEntityId"); e.Property<string>("Code").IsRequired(); e.Property<string>("Version").IsRequired(); e.Property<decimal>("Amount").HasPrecision(12,2); e.HasKey("Id"); e.HasIndex("ServiceRequestEntityId"); e.ToTable("Fees"); });
        b.Entity("PaymentApi.PaymentEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<Guid>("ServiceRequestEntityId"); e.Property<string>("Reference").IsRequired(); e.Property<decimal>("Amount").HasPrecision(12,2); e.Property<string>("Currency").IsRequired(); e.Property<string>("ReturnUrl").IsRequired(); e.Property<string>("Status").IsRequired(); e.Property<DateTimeOffset>("Created"); e.HasKey("Id"); e.HasIndex("Reference").IsUnique(); e.HasIndex("ServiceRequestEntityId"); e.ToTable("Payments"); });
        b.Entity("PaymentApi.StatusHistoryEntity", e => { e.Property<Guid>("Id").ValueGeneratedOnAdd(); e.Property<Guid>("PaymentEntityId"); e.Property<string>("Status").IsRequired(); e.Property<DateTimeOffset>("Created"); e.HasKey("Id"); e.HasIndex("PaymentEntityId"); e.ToTable("StatusHistory"); });
        b.Entity("PaymentApi.FeeEntity", e => e.HasOne("PaymentApi.ServiceRequestEntity", null).WithMany("Fees").HasForeignKey("ServiceRequestEntityId").OnDelete(DeleteBehavior.Cascade).IsRequired());
        b.Entity("PaymentApi.PaymentEntity", e => e.HasOne("PaymentApi.ServiceRequestEntity", "ServiceRequest").WithMany("Payments").HasForeignKey("ServiceRequestEntityId").OnDelete(DeleteBehavior.Cascade).IsRequired());
        b.Entity("PaymentApi.StatusHistoryEntity", e => e.HasOne("PaymentApi.PaymentEntity", null).WithMany("History").HasForeignKey("PaymentEntityId").OnDelete(DeleteBehavior.Cascade).IsRequired());
        b.Entity("PaymentApi.LegacyServiceRequestDetailsEntity", e => e.HasOne("PaymentApi.ServiceRequestEntity", "ServiceRequest").WithOne("LegacyDetails").HasForeignKey("PaymentApi.LegacyServiceRequestDetailsEntity", "ServiceRequestEntityId").OnDelete(DeleteBehavior.Cascade).IsRequired());
        b.Entity("PaymentApi.LegacyPaymentDetailsEntity", e => e.HasOne("PaymentApi.PaymentEntity", "Payment").WithOne("LegacyDetails").HasForeignKey("PaymentApi.LegacyPaymentDetailsEntity", "PaymentEntityId").OnDelete(DeleteBehavior.Cascade).IsRequired());
    }
}
