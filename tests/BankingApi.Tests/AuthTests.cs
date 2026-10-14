using System.Net;
using System.Net.Http.Json;
using BankingApi.Api.Contracts;

namespace BankingApi.Tests;

public class AuthTests(BankingApiFactory factory) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Register_then_login_returns_a_token()
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Ana Perez", email, "Password123!"));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email.ToUpperInvariant(), "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(BankingApiFactory.Json);
        Assert.False(string.IsNullOrEmpty(auth!.AccessToken));
        Assert.Equal("Customer", auth.User.Role);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_409()
    {
        var client = factory.CreateClient();
        var request = new RegisterRequest("Ana Perez", $"{Guid.NewGuid():N}@example.com", "Password123!");

        await client.PostAsJsonAsync("/api/auth/register", request);
        var second = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("email_taken", await second.ErrorCodeAsync());
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Ana Perez", email, "Password123!"));

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Weak_password_is_rejected_with_400()
    {
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Ana Perez", $"{Guid.NewGuid():N}@example.com", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
    }

    [Fact]
    public async Task Account_endpoints_require_a_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
