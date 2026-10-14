namespace BankingApi.Api.Domain;

/// <summary>Base type for errors the API reports to the client as problem details.</summary>
public abstract class AppException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
    public abstract int StatusCode { get; }
}

/// <summary>The request is valid but breaks a banking rule (insufficient funds, frozen account...).</summary>
public class BusinessRuleException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status422UnprocessableEntity;
}

public class NotFoundException(string message) : AppException("not_found", message)
{
    public override int StatusCode => StatusCodes.Status404NotFound;
}

public class ConflictException(string code, string message) : AppException(code, message)
{
    public override int StatusCode => StatusCodes.Status409Conflict;
}

public class UnauthorizedException(string message) : AppException("unauthorized", message)
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
}
