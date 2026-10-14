using BankingApi.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Api.Controllers;

public static class ControllerExtensions
{
    /// <summary>
    /// 201 for a new operation; 200 plus an <c>Idempotent-Replayed: true</c> header when the
    /// same Idempotency-Key was already processed and the original result is returned.
    /// </summary>
    public static ActionResult<TransactionResponse> ToMoneyResult(this ControllerBase controller, OperationResult result)
    {
        if (!result.Replayed)
            return controller.StatusCode(StatusCodes.Status201Created, result.Transaction);

        controller.Response.Headers["Idempotent-Replayed"] = "true";
        return controller.Ok(result.Transaction);
    }
}
