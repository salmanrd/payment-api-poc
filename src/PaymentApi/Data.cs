using Microsoft.EntityFrameworkCore;

namespace PaymentApi;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<ServiceRequestEntity> ServiceRequests => Set<ServiceRequestEntity>();
    public DbSet<FeeEntity> Fees => Set<FeeEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<StatusHistoryEntity> StatusHistory => Set<StatusHistoryEntity>();
    public DbSet<ArchivedTransactionEntity> ArchivedTransactions => Set<ArchivedTransactionEntity>();
    public DbSet<LegacyServiceRequestDetailsEntity> LegacyServiceRequestDetails => Set<LegacyServiceRequestDetailsEntity>();
    public DbSet<LegacyPaymentDetailsEntity> LegacyPaymentDetails => Set<LegacyPaymentDetailsEntity>();
    public DbSet<TransactionEntity> Transactions => Set<TransactionEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ServiceRequestEntity>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<ServiceRequestEntity>().HasIndex(x => x.CcdCaseNumber);
        b.Entity<PaymentEntity>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<FeeEntity>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<PaymentEntity>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<TransactionEntity>().HasKey(x => x.TransactionId);
        b.Entity<TransactionEntity>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<ArchivedTransactionEntity>().Property(x => x.FeeTotal).HasPrecision(12, 2);
        b.Entity<ArchivedTransactionEntity>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<ArchivedTransactionEntity>().HasIndex(x => new { x.LegacySystem, x.TransactionId }).IsUnique();
        b.Entity<LegacyServiceRequestDetailsEntity>().HasIndex(x => new { x.LegacySystem, x.TransactionId }).IsUnique();
        b.Entity<LegacyServiceRequestDetailsEntity>()
            .HasOne(x => x.ServiceRequest).WithOne(x => x.LegacyDetails)
            .HasForeignKey<LegacyServiceRequestDetailsEntity>(x => x.ServiceRequestEntityId)
            .IsRequired().OnDelete(DeleteBehavior.Cascade);
        b.Entity<LegacyPaymentDetailsEntity>().HasIndex(x => x.PaymentEntityId).IsUnique();
        b.Entity<LegacyPaymentDetailsEntity>().HasIndex(x => new { x.LegacySystem, x.TransactionId }).IsUnique();
        // Legacy payment references are guaranteed unique within each source system.
        b.Entity<LegacyPaymentDetailsEntity>().HasIndex(x => new { x.LegacySystem, x.LegacyPaymentReference }).IsUnique();
        b.Entity<LegacyPaymentDetailsEntity>()
            .HasOne(x => x.Payment).WithOne(x => x.LegacyDetails)
            .HasForeignKey<LegacyPaymentDetailsEntity>(x => x.PaymentEntityId)
            .IsRequired().OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ServiceRequestEntity
{
    public Guid Id { get; set; } public string Reference { get; set; } = ""; public string CallbackUrl { get; set; } = "";
    public string? CaseReference { get; set; } public string CcdCaseNumber { get; set; } = ""; public DateTimeOffset Created { get; set; }
    public List<FeeEntity> Fees { get; set; } = []; public List<PaymentEntity> Payments { get; set; } = [];
    public LegacyServiceRequestDetailsEntity? LegacyDetails { get; set; }
}
public sealed class LegacyServiceRequestDetailsEntity
{
    public Guid Id { get; set; }
    public Guid ServiceRequestEntityId { get; set; }
    public ServiceRequestEntity ServiceRequest { get; set; } = null!;
    public string LegacySystem { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
}
public sealed class ArchivedTransactionEntity
{
    public Guid Id { get; set; }
    public string LegacySystem { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public string? CaseReference { get; set; }
    public string? CcdCaseNumber { get; set; }
    public decimal? FeeTotal { get; set; }
    public string? FeeTransactionId { get; set; }
    public string? LegacyPaymentReference { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
    public string? ProviderTransactionId { get; set; }
}
public sealed class FeeEntity { public Guid Id { get; set; } public Guid ServiceRequestEntityId { get; set; } public string Code { get; set; } = ""; public string Version { get; set; } = ""; public decimal Amount { get; set; } }
public sealed class PaymentEntity
{
    public Guid Id { get; set; } public Guid ServiceRequestEntityId { get; set; } public ServiceRequestEntity ServiceRequest { get; set; } = null!;
    public string Reference { get; set; } = ""; public decimal Amount { get; set; } public string Currency { get; set; } = "GBP";
    public string ReturnUrl { get; set; } = ""; public string Status { get; set; } = "Initiated"; public DateTimeOffset Created { get; set; }
    public List<StatusHistoryEntity> History { get; set; } = [];
    public LegacyPaymentDetailsEntity? LegacyDetails { get; set; }
}
public sealed class LegacyPaymentDetailsEntity
{
    public Guid Id { get; set; }
    public Guid PaymentEntityId { get; set; }
    public PaymentEntity Payment { get; set; } = null!;
    public string LegacySystem { get; set; } = "";
    public string TransactionId { get; set; } = "";
    public string LegacyPaymentReference { get; set; } = "";
    public string? ProviderTransactionId { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
}
public sealed class TransactionEntity
{
    public Guid TransactionId { get; set; }
    public string CaseNo { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public int TransactionMethodId { get; set; }
    public DateTimeOffset TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public string TransactionStatus { get; set; } = "";
    public string? OriginalPaymentReference { get; set; }
    public string PaymentReference { get; set; } = "";
}
public sealed class StatusHistoryEntity { public Guid Id { get; set; } public Guid PaymentEntityId { get; set; } public string Status { get; set; } = ""; public DateTimeOffset Created { get; set; } }
