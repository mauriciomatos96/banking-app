using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BankingApi.Api.Contracts;
using BankingApi.Api.Data;
using BankingApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Api.Services;

/// <summary>
/// Deposits, withdrawals and transfers. Every operation:
/// <list type="bullet">
/// <item>runs inside a database transaction, so balances and ledger entries are saved together or not at all;</item>
/// <item>is protected by an Idempotency-Key, so a retried request never moves money twice;</item>
/// <item>uses optimistic concurrency on the account balance and retries if another request changed it first.</item>
/// </list>
/// </summary>
public class MoneyMovementService(BankingDbContext db, TimeProvider clock, ILogger<MoneyMovementService> logger)
{
    private const int MaxAttempts = 3;

    public Task<OperationResult> DepositAsync(
        Guid userId, Guid accountId, MoneyRequest request, string idempotencyKey, CancellationToken ct)
    {
        var amount = ValidateAmount(request.Amount);
        var hash = Hash("deposit", accountId, amount, request.Description);

        return ExecuteAsync(userId, idempotencyKey, hash, async () =>
        {
            var account = await LoadOwnedAsync(userId, accountId, ct);
            account.Credit(amount);
            return AddEntry(account, TransactionType.Deposit, amount, request.Description);
        }, ct);
    }

    public Task<OperationResult> WithdrawAsync(
        Guid userId, Guid accountId, MoneyRequest request, string idempotencyKey, CancellationToken ct)
    {
        var amount = ValidateAmount(request.Amount);
        var hash = Hash("withdrawal", accountId, amount, request.Description);

        return ExecuteAsync(userId, idempotencyKey, hash, async () =>
        {
            var account = await LoadOwnedAsync(userId, accountId, ct);
            account.Debit(amount);
            return AddEntry(account, TransactionType.Withdrawal, amount, request.Description);
        }, ct);
    }

    public Task<OperationResult> TransferAsync(
        Guid userId, TransferRequest request, string idempotencyKey, CancellationToken ct)
    {
        var amount = ValidateAmount(request.Amount);
        var hash = Hash("transfer", request.FromAccountId, amount, request.Description, request.ToAccountNumber);

        return ExecuteAsync(userId, idempotencyKey, hash, async () =>
        {
            var source = await LoadOwnedAsync(userId, request.FromAccountId, ct);
            var destination = await db.Accounts.SingleOrDefaultAsync(a => a.AccountNumber == request.ToAccountNumber, ct)
                ?? throw new NotFoundException("Destination account not found.");

            if (destination.Id == source.Id)
                throw new BusinessRuleException("same_account", "Source and destination accounts must be different.");
            if (destination.Currency != source.Currency)
                throw new BusinessRuleException("currency_mismatch",
                    $"Cannot transfer {source.Currency} to an account in {destination.Currency}.");

            source.Debit(amount);
            destination.Credit(amount);

            var transferId = Guid.NewGuid();
            var outgoing = AddEntry(source, TransactionType.TransferOut, amount, request.Description, transferId, destination.AccountNumber);
            AddEntry(destination, TransactionType.TransferIn, amount, request.Description, transferId, source.AccountNumber);
            return outgoing;
        }, ct);
    }

    private async Task<OperationResult> ExecuteAsync(
        Guid userId, string idempotencyKey, string requestHash, Func<Task<Transaction>> operation, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            throw new BusinessRuleException("invalid_idempotency_key", "The Idempotency-Key header is required (max 100 characters).");

        if (await TryReplayAsync(userId, idempotencyKey, requestHash, ct) is { } replay)
            return replay;

        for (var attempt = 1; ; attempt++)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var entry = await operation();
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    UserId = userId,
                    Key = idempotencyKey,
                    RequestHash = requestHash,
                    TransactionId = entry.Id,
                    CreatedAt = Now()
                });

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new OperationResult(TransactionResponse.From(entry), Replayed: false);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // Another request changed one of the balances after we read it. Start over with fresh data.
                await tx.RollbackAsync(ct);
                logger.LogInformation("Concurrency conflict on attempt {Attempt}; retrying.", attempt);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("concurrent_update", "The account was modified by another operation. Please retry.");
            }
            catch (DbUpdateException)
            {
                // Most likely a simultaneous request with the same Idempotency-Key won the unique index.
                await tx.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                if (await TryReplayAsync(userId, idempotencyKey, requestHash, ct) is { } concurrentReplay)
                    return concurrentReplay;
                throw;
            }
        }
    }

    private async Task<OperationResult?> TryReplayAsync(Guid userId, string key, string requestHash, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(r => r.UserId == userId && r.Key == key, ct);
        if (record is null)
            return null;

        if (record.RequestHash != requestHash)
            throw new BusinessRuleException("idempotency_key_reused",
                "This Idempotency-Key was already used for a different request.");

        var original = await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == record.TransactionId, ct);
        return new OperationResult(TransactionResponse.From(original), Replayed: true);
    }

    private async Task<Account> LoadOwnedAsync(Guid userId, Guid accountId, CancellationToken ct) =>
        await db.Accounts.SingleOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId, ct)
        ?? throw new NotFoundException("Account not found.");

    private Transaction AddEntry(
        Account account, TransactionType type, decimal amount, string? description,
        Guid? transferId = null, string? counterparty = null)
    {
        var entry = new Transaction
        {
            AccountId = account.Id,
            Type = type,
            Amount = amount,
            BalanceAfter = account.Balance,
            Description = description?.Trim(),
            TransferId = transferId,
            CounterpartyAccountNumber = counterparty,
            CreatedAt = Now()
        };
        db.Transactions.Add(entry);
        return entry;
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("invalid_amount", "The amount must be greater than zero.");
        if (decimal.Round(amount, 2) != amount)
            throw new BusinessRuleException("invalid_amount", "The amount can have at most two decimal places.");
        return decimal.Round(amount, 2);
    }

    private static string Hash(string operation, Guid accountId, decimal amount, string? description, string? destination = null)
    {
        var payload = string.Join('|', operation, accountId,
            amount.ToString("0.00", CultureInfo.InvariantCulture), description?.Trim() ?? "", destination ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
