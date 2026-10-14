namespace BankingApi.Api.Domain;

/// <summary>
/// Remembers which operation an Idempotency-Key produced, so a retried request
/// returns the original result instead of moving money twice.
/// </summary>
public class IdempotencyRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string Key { get; set; }

    /// <summary>SHA-256 of the operation and its payload, to detect a key reused for a different request.</summary>
    public required string RequestHash { get; set; }

    /// <summary>The ledger entry returned to the caller (for transfers, the TransferOut entry).</summary>
    public Guid TransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
