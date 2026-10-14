namespace BankingApi.Api.Domain;

public enum AccountStatus
{
    Active,
    Frozen
}

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Public 10-digit number used by other customers to send transfers.</summary>
    public required string AccountNumber { get; set; }

    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>ISO 4217 code, e.g. DOP or USD.</summary>
    public required string Currency { get; set; }

    public decimal Balance { get; private set; }
    public AccountStatus Status { get; set; } = AccountStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optimistic concurrency token. It changes on every balance update, so two requests
    /// that read the same balance cannot both write a new one.
    /// </summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    public void Credit(decimal amount)
    {
        EnsureActive();
        Balance += amount;
        Version = Guid.NewGuid();
    }

    public void Debit(decimal amount)
    {
        EnsureActive();
        if (Balance < amount)
            throw new BusinessRuleException("insufficient_funds", "The account does not have enough funds for this operation.");

        Balance -= amount;
        Version = Guid.NewGuid();
    }

    private void EnsureActive()
    {
        if (Status != AccountStatus.Active)
            throw new BusinessRuleException("account_frozen", $"Account {AccountNumber} is frozen.");
    }
}
