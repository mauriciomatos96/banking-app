using System.Net.Http.Json;
using System.Text.Json;
using BankingApi.Api.Contracts;

namespace BankingApi.Tests;

public static class ApiClientExtensions
{
    public static async Task<AccountResponse> CreateAccountAsync(this HttpClient client, string currency = "DOP")
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest(currency));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountResponse>(BankingApiFactory.Json))!;
    }

    public static async Task<AccountResponse> GetAccountAsync(this HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<AccountResponse>($"/api/accounts/{id}", BankingApiFactory.Json))!;

    public static Task<HttpResponseMessage> DepositAsync(this HttpClient client, Guid accountId, decimal amount, string? key = null) =>
        client.SendMoneyAsync($"/api/accounts/{accountId}/deposits", new MoneyRequest(amount, "Test deposit"), key);

    public static Task<HttpResponseMessage> WithdrawAsync(this HttpClient client, Guid accountId, decimal amount, string? key = null) =>
        client.SendMoneyAsync($"/api/accounts/{accountId}/withdrawals", new MoneyRequest(amount, "Test withdrawal"), key);

    public static Task<HttpResponseMessage> TransferAsync(
        this HttpClient client, Guid fromAccountId, string toAccountNumber, decimal amount, string? key = null) =>
        client.SendMoneyAsync("/api/transfers", new TransferRequest(fromAccountId, toAccountNumber, amount, "Test transfer"), key);

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static Task<HttpResponseMessage> SendMoneyAsync(this HttpClient client, string url, object body, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }
}
