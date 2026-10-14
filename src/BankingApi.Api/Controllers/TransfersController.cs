using BankingApi.Api.Auth;
using BankingApi.Api.Contracts;
using BankingApi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transfers")]
[Produces("application/json")]
public class TransfersController(MoneyMovementService money) : ControllerBase
{
    /// <summary>
    /// Moves money from one of your accounts to any account, by account number.
    /// Both balances and both ledger entries are saved in a single database transaction.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TransactionResponse>> Create(
        TransferRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey, CancellationToken ct) =>
        this.ToMoneyResult(await money.TransferAsync(User.GetUserId(), request, idempotencyKey, ct));
}
