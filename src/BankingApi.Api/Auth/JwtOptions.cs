using System.ComponentModel.DataAnnotations;

namespace BankingApi.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = "BankingApi";
    [Required] public string Audience { get; set; } = "BankingApi";

    /// <summary>HMAC-SHA256 signing key. Must be at least 32 characters; set it through an environment variable outside development.</summary>
    [Required, MinLength(32)] public string SigningKey { get; set; } = "";

    [Range(1, 1440)] public int ExpirationMinutes { get; set; } = 60;
}
