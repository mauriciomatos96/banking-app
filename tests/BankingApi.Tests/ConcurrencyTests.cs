using BankingApi.Api.Data;
using BankingApi.Api.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Tests;

/// <summary>
/// Proves the optimistic concurrency token on <see cref="Account"/>: if two operations read the
/// same balance, only the first one can save; the second gets a conflict instead of a lost update.
/// </summary>
public class ConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<BankingDbContext> _options;

    public ConcurrencyTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<BankingDbContext>().UseSqlite(_connection).Options;
        using var db = new BankingDbContext(_options);
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Saving_a_stale_balance_throws_a_concurrency_exception()
    {
        var accountId = await CreateAccountWithBalanceAsync(100m);

        await using var first = new BankingDbContext(_options);
        await using var second = new BankingDbContext(_options);
        var a = await first.Accounts.SingleAsync(x => x.Id == accountId);
        var b = await second.Accounts.SingleAsync(x => x.Id == accountId);

        a.Debit(80m);
        await first.SaveChangesAsync();

        b.Debit(80m); // Still sees 100, but the real balance is now 20.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using var check = new BankingDbContext(_options);
        Assert.Equal(20m, (await check.Accounts.SingleAsync(x => x.Id == accountId)).Balance);
    }

    [Fact]
    public void Debit_beyond_the_balance_is_a_business_rule_error()
    {
        var account = new Account { AccountNumber = "1234567890", Currency = "DOP" };
        account.Credit(50m);

        var error = Assert.Throws<BusinessRuleException>(() => account.Debit(50.01m));

        Assert.Equal("insufficient_funds", error.Code);
        Assert.Equal(50m, account.Balance);
    }

    private async Task<Guid> CreateAccountWithBalanceAsync(decimal balance)
    {
        await using var db = new BankingDbContext(_options);
        var user = new User { FullName = "Test", Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "x" };
        var account = new Account { Owner = user, AccountNumber = "1234567890", Currency = "DOP" };
        account.Credit(balance);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    public void Dispose() => _connection.Dispose();
}
