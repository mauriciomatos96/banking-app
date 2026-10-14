using System.Security.Cryptography;
using BankingApi.Api.Contracts;
using BankingApi.Api.Data;
using BankingApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Api.Services;

public class AccountService(BankingDbContext db)
{
    public async Task<AccountResponse> CreateAsync(Guid userId, CreateAccountRequest request, CancellationToken ct)
    {
        var account = new Account
        {
            OwnerId = userId,
            AccountNumber = await GenerateAccountNumberAsync(ct),
            Currency = request.Currency
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        return AccountResponse.From(account);
    }

    public async Task<IReadOnlyList<AccountResponse>> ListForUserAsync(Guid userId, CancellationToken ct)
    {
        var accounts = await db.Accounts.AsNoTracking()
            .Where(a => a.OwnerId == userId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
        return accounts.Select(AccountResponse.From).ToList();
    }

    public async Task<AccountResponse> GetForUserAsync(Guid userId, Guid accountId, CancellationToken ct) =>
        AccountResponse.From(await GetOwnedAsync(userId, accountId, ct));

    public async Task<PagedResponse<TransactionResponse>> GetHistoryAsync(
        Guid userId, Guid accountId, TransactionQuery query, CancellationToken ct)
    {
        await GetOwnedAsync(userId, accountId, ct);

        var transactions = db.Transactions.AsNoTracking().Where(t => t.AccountId == accountId);
        if (query.Type is { } type)
            transactions = transactions.Where(t => t.Type == type);
        if (query.From is { } from)
            transactions = transactions.Where(t => t.CreatedAt >= AsUtc(from));
        if (query.To is { } to)
            transactions = transactions.Where(t => t.CreatedAt <= AsUtc(to));

        var total = await transactions.CountAsync(ct);
        var page = await transactions
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResponse<TransactionResponse>(
            page.Select(TransactionResponse.From).ToList(), query.Page, query.PageSize, total);
    }

    // ---------- Admin ----------

    public async Task<PagedResponse<AccountResponse>> ListAllAsync(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.Accounts.CountAsync(ct);
        var accounts = await db.Accounts.AsNoTracking()
            .OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResponse<AccountResponse>(accounts.Select(AccountResponse.From).ToList(), page, pageSize, total);
    }

    public async Task<AccountResponse> SetStatusAsync(Guid accountId, AccountStatus status, CancellationToken ct)
    {
        var account = await db.Accounts.FindAsync([accountId], ct)
            ?? throw new NotFoundException("Account not found.");
        account.Status = status;
        await db.SaveChangesAsync(ct);
        return AccountResponse.From(account);
    }

    /// <summary>
    /// Returns the account only if it belongs to the user. Someone else's account is reported
    /// as "not found" rather than "forbidden", so account ids cannot be probed.
    /// </summary>
    private async Task<Account> GetOwnedAsync(Guid userId, Guid accountId, CancellationToken ct) =>
        await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId, ct)
        ?? throw new NotFoundException("Account not found.");

    private async Task<string> GenerateAccountNumberAsync(CancellationToken ct)
    {
        while (true)
        {
            var number = RandomNumberGenerator.GetInt32(1_000_000_000, int.MaxValue).ToString("D10");
            if (!await db.Accounts.AnyAsync(a => a.AccountNumber == number, ct))
                return number;
        }
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
