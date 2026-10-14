using BankingApi.Api.Auth;
using BankingApi.Api.Contracts;
using BankingApi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
[Produces("application/json")]
public class AccountsController(AccountService accounts, MoneyMovementService money) : ControllerBase
{
    /// <summary>Lists the accounts of the signed-in user.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> List(CancellationToken ct) =>
        Ok(await accounts.ListForUserAsync(User.GetUserId(), ct));

    /// <summary>Opens a new account with a zero balance.</summary>
    [HttpPost]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AccountResponse>> Create(CreateAccountRequest request, CancellationToken ct)
    {
        var account = await accounts.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = account.Id }, account);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> Get(Guid id, CancellationToken ct) =>
        Ok(await accounts.GetForUserAsync(User.GetUserId(), id, ct));

    /// <summary>Movement history, newest first, with optional filters by type and date range.</summary>
    [HttpGet("{id:guid}/transactions")]
    public async Task<ActionResult<PagedResponse<TransactionResponse>>> History(
        Guid id, [FromQuery] TransactionQuery query, CancellationToken ct) =>
        Ok(await accounts.GetHistoryAsync(User.GetUserId(), id, query, ct));

    /// <summary>Adds money to the account. Send a unique Idempotency-Key per operation; retries with the same key are safe.</summary>
    [HttpPost("{id:guid}/deposits")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Deposit(
        Guid id, MoneyRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        this.ToMoneyResult(await money.DepositAsync(User.GetUserId(), id, request, idempotencyKey, ct));

    /// <summary>Takes money out of the account. Fails with 422 if the balance is not enough.</summary>
    [HttpPost("{id:guid}/withdrawals")]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Withdraw(
        Guid id, MoneyRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        this.ToMoneyResult(await money.WithdrawAsync(User.GetUserId(), id, request, idempotencyKey, ct));
}
