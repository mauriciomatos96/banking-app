using BankingApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Api.Data;

public class BankingDbContext(DbContextOptions<BankingDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.FullName).HasMaxLength(150);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        model.Entity<Account>(e =>
        {
            e.HasIndex(a => a.AccountNumber).IsUnique();
            e.Property(a => a.AccountNumber).HasMaxLength(10);
            e.Property(a => a.Currency).HasMaxLength(3);
            e.Property(a => a.Balance).HasPrecision(18, 2);
            e.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(a => a.Version).IsConcurrencyToken();
            e.HasOne(a => a.Owner).WithMany(u => u.Accounts).HasForeignKey(a => a.OwnerId);
        });

        model.Entity<Transaction>(e =>
        {
            e.HasIndex(t => new { t.AccountId, t.CreatedAt });
            e.HasIndex(t => t.TransferId);
            e.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.BalanceAfter).HasPrecision(18, 2);
            e.Property(t => t.Description).HasMaxLength(200);
            e.Property(t => t.CounterpartyAccountNumber).HasMaxLength(10);
            e.HasOne(t => t.Account).WithMany().HasForeignKey(t => t.AccountId);
        });

        model.Entity<IdempotencyRecord>(e =>
        {
            // The unique index is what makes two simultaneous requests with the same key safe:
            // only one of them can insert its record.
            e.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
            e.Property(r => r.Key).HasMaxLength(100);
            e.Property(r => r.RequestHash).HasMaxLength(64);
        });
    }
}
