using BankingApi.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BankingApi.Api.Infrastructure;

/// <summary>Turns <see cref="AppException"/> into RFC 7807 problem details with a machine-readable error code.</summary>
public class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException app)
            return false;

        context.Response.StatusCode = app.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = app.StatusCode,
                Title = app.Message,
                Extensions = { ["code"] = app.Code }
            }
        });
    }
}
