using BankingApi.Api.Auth;
using BankingApi.Api.Contracts;
using BankingApi.Api.Data;
using BankingApi.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.Api.Services;

public class AuthService(BankingDbContext db, TokenService tokens)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("email_taken", "An account with this email already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        // Same message for "no such user" and "wrong password", so the API does not reveal which emails are registered.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, new UserResponse(user.Id, user.FullName, user.Email, user.Role.ToString()));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
