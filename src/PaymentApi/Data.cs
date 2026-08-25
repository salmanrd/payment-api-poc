using Microsoft.EntityFrameworkCore;

namespace PaymentApi;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<ServiceRequestEntity> ServiceRequests => Set<ServiceRequestEntity>();
    public DbSet<FeeEntity> Fees => Set<FeeEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<StatusHistoryEntity> StatusHistory => Set<StatusHistoryEntity>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ServiceRequestEntity>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<ServiceRequestEntity>().HasIndex(x => x.CcdCaseNumber).IsUnique();
        b.Entity<PaymentEntity>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<FeeEntity>().Property(x => x.Amount).HasPrecision(12, 2);
        b.Entity<PaymentEntity>().Property(x => x.Amount).HasPrecision(12, 2);
    }
}

public sealed class ServiceRequestEntity
{
    public Guid Id { get; set; } public string Reference { get; set; } = ""; public string CallbackUrl { get; set; } = "";
    public string? CaseReference { get; set; } public string CcdCaseNumber { get; set; } = ""; public DateTimeOffset Created { get; set; }
    public List<FeeEntity> Fees { get; set; } = []; public List<PaymentEntity> Payments { get; set; } = [];
}
public sealed class FeeEntity { public Guid Id { get; set; } public Guid ServiceRequestEntityId { get; set; } public string Code { get; set; } = ""; public string Version { get; set; } = ""; public decimal Amount { get; set; } }
public sealed class PaymentEntity
{
    public Guid Id { get; set; } public Guid ServiceRequestEntityId { get; set; } public ServiceRequestEntity ServiceRequest { get; set; } = null!;
    public string Reference { get; set; } = ""; public decimal Amount { get; set; } public string Currency { get; set; } = "GBP";
    public string ReturnUrl { get; set; } = ""; public string Status { get; set; } = "Initiated"; public DateTimeOffset Created { get; set; }
    public List<StatusHistoryEntity> History { get; set; } = [];
}
public sealed class StatusHistoryEntity { public Guid Id { get; set; } public Guid PaymentEntityId { get; set; } public string Status { get; set; } = ""; public DateTimeOffset Created { get; set; } }
