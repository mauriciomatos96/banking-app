using System.ComponentModel.DataAnnotations;
using BankingApi.Api.Contracts;
using BankingApi.Api.Domain;
using BankingApi.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/accounts")]
[Produces("application/json")]
public class AdminController(AccountService accounts) : ControllerBase
{
    /// <summary>Lists every account in the bank (admin only).</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AccountResponse>>> List(
        [FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await accounts.ListAllAsync(page, pageSize, ct));

    /// <summary>Blocks all deposits, withdrawals and transfers on the account.</summary>
    [HttpPost("{id:guid}/freeze")]
    public async Task<ActionResult<AccountResponse>> Freeze(Guid id, CancellationToken ct) =>
        Ok(await accounts.SetStatusAsync(id, AccountStatus.Frozen, ct));

    [HttpPost("{id:guid}/unfreeze")]
    public async Task<ActionResult<AccountResponse>> Unfreeze(Guid id, CancellationToken ct) =>
        Ok(await accounts.SetStatusAsync(id, AccountStatus.Active, ct));
}
