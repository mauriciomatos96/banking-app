namespace BankingApi.Api.Domain;

public enum TransactionType
{
    Deposit,
    Withdrawal,
    TransferIn,
    TransferOut
}

/// <summary>
/// One immutable ledger entry. A transfer produces two entries (TransferOut and TransferIn)
/// that share the same <see cref="TransferId"/>.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>Always positive; <see cref="Type"/> says whether money came in or out.</summary>
    public decimal Amount { get; set; }

    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }

    public Guid? TransferId { get; set; }
    public string? CounterpartyAccountNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
