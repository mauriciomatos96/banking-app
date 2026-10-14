using System.ComponentModel.DataAnnotations;
using BankingApi.Api.Domain;

namespace BankingApi.Api.Contracts;

// ---------- Auth ----------

public record RegisterRequest(
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(100, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserResponse User);

public record UserResponse(Guid Id, string FullName, string Email, string Role);

// ---------- Accounts ----------

public record CreateAccountRequest(
    [Required, RegularExpression("^(DOP|USD|EUR)$", ErrorMessage = "Currency must be DOP, USD or EUR.")] string Currency);

public record AccountResponse(
    Guid Id, string AccountNumber, string Currency, decimal Balance, string Status, DateTime CreatedAt)
{
    public static AccountResponse From(Account a) =>
        new(a.Id, a.AccountNumber, a.Currency, a.Balance, a.Status.ToString(), a.CreatedAt);
}

// ---------- Money movements ----------

public record MoneyRequest(
    [Range(typeof(decimal), "0.01", "1000000")] decimal Amount,
    [StringLength(200)] string? Description);

public record TransferRequest(
    [Required] Guid FromAccountId,
    [Required, RegularExpression(@"^\d{10}$", ErrorMessage = "Account number must have 10 digits.")] string ToAccountNumber,
    [Range(typeof(decimal), "0.01", "1000000")] decimal Amount,
    [StringLength(200)] string? Description);

public record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    decimal BalanceAfter,
    string? Description,
    Guid? TransferId,
    string? CounterpartyAccountNumber,
    DateTime CreatedAt)
{
    public static TransactionResponse From(Transaction t) =>
        new(t.Id, t.AccountId, t.Type.ToString(), t.Amount, t.BalanceAfter, t.Description,
            t.TransferId, t.CounterpartyAccountNumber, t.CreatedAt);
}

public record TransactionQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    public TransactionType? Type { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Result of a money movement, plus whether it was replayed from an earlier request with the same Idempotency-Key.</summary>
public record OperationResult(TransactionResponse Transaction, bool Replayed);
