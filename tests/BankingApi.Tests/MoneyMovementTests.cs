using System.Net;
using System.Net.Http.Json;
using BankingApi.Api.Contracts;

namespace BankingApi.Tests;

public class MoneyMovementTests(BankingApiFactory factory) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Deposit_and_withdrawal_update_the_balance()
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();

        Assert.Equal(HttpStatusCode.Created, (await client.DepositAsync(account.Id, 1000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.WithdrawAsync(account.Id, 250.50m)).StatusCode);

        Assert.Equal(749.50m, (await client.GetAccountAsync(account.Id)).Balance);
    }

    [Fact]
    public async Task Withdrawing_more_than_the_balance_fails_and_changes_nothing()
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();
        await client.DepositAsync(account.Id, 100m);

        var response = await client.WithdrawAsync(account.Id, 100.01m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("insufficient_funds", await response.ErrorCodeAsync());
        Assert.Equal(100m, (await client.GetAccountAsync(account.Id)).Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10.001)]
    public async Task Invalid_amounts_are_rejected(decimal amount)
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();

        var response = await client.DepositAsync(account.Id, amount);

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity,
            $"Expected 400 or 422 but got {(int)response.StatusCode}.");
        Assert.Equal(0m, (await client.GetAccountAsync(account.Id)).Balance);
    }

    [Fact]
    public async Task Transfer_moves_money_and_records_both_sides()
    {
        var alice = await factory.CreateCustomerClientAsync("Alice");
        var bob = await factory.CreateCustomerClientAsync("Bob");
        var aliceAccount = await alice.CreateAccountAsync();
        var bobAccount = await bob.CreateAccountAsync();
        await alice.DepositAsync(aliceAccount.Id, 500m);

        var response = await alice.TransferAsync(aliceAccount.Id, bobAccount.AccountNumber, 200m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(300m, (await alice.GetAccountAsync(aliceAccount.Id)).Balance);
        Assert.Equal(200m, (await bob.GetAccountAsync(bobAccount.Id)).Balance);

        var outgoing = await response.Content.ReadFromJsonAsync<TransactionResponse>(BankingApiFactory.Json);
        var bobHistory = await bob.GetFromJsonAsync<PagedResponse<TransactionResponse>>(
            $"/api/accounts/{bobAccount.Id}/transactions", BankingApiFactory.Json);
        var incoming = Assert.Single(bobHistory!.Items);
        Assert.Equal("TransferIn", incoming.Type);
        Assert.Equal(outgoing!.TransferId, incoming.TransferId);
        Assert.Equal(aliceAccount.AccountNumber, incoming.CounterpartyAccountNumber);
    }

    [Fact]
    public async Task Transfer_between_different_currencies_is_rejected()
    {
        var client = await factory.CreateCustomerClientAsync();
        var pesos = await client.CreateAccountAsync("DOP");
        var dollars = await client.CreateAccountAsync("USD");
        await client.DepositAsync(pesos.Id, 1000m);

        var response = await client.TransferAsync(pesos.Id, dollars.AccountNumber, 100m);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("currency_mismatch", await response.ErrorCodeAsync());
        Assert.Equal(1000m, (await client.GetAccountAsync(pesos.Id)).Balance);
    }

    [Fact]
    public async Task Retrying_with_the_same_idempotency_key_does_not_charge_twice()
    {
        var alice = await factory.CreateCustomerClientAsync();
        var bob = await factory.CreateCustomerClientAsync();
        var aliceAccount = await alice.CreateAccountAsync();
        var bobAccount = await bob.CreateAccountAsync();
        await alice.DepositAsync(aliceAccount.Id, 500m);
        var key = Guid.NewGuid().ToString();

        var first = await alice.TransferAsync(aliceAccount.Id, bobAccount.AccountNumber, 100m, key);
        var retry = await alice.TransferAsync(aliceAccount.Id, bobAccount.AccountNumber, 100m, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("true", retry.Headers.GetValues("Idempotent-Replayed").Single());
        var firstTx = await first.Content.ReadFromJsonAsync<TransactionResponse>(BankingApiFactory.Json);
        var retryTx = await retry.Content.ReadFromJsonAsync<TransactionResponse>(BankingApiFactory.Json);
        Assert.Equal(firstTx!.Id, retryTx!.Id);
        Assert.Equal(400m, (await alice.GetAccountAsync(aliceAccount.Id)).Balance);
    }

    [Fact]
    public async Task Concurrent_requests_with_the_same_idempotency_key_apply_once()
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();
        var key = Guid.NewGuid().ToString();

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.DepositAsync(account.Id, 50m, key)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"Got {(int)r.StatusCode}"));
        Assert.Equal(50m, (await client.GetAccountAsync(account.Id)).Balance);
    }

    [Fact]
    public async Task Reusing_an_idempotency_key_for_a_different_request_is_rejected()
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();
        var key = Guid.NewGuid().ToString();
        await client.DepositAsync(account.Id, 100m, key);

        var response = await client.DepositAsync(account.Id, 999m, key);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("idempotency_key_reused", await response.ErrorCodeAsync());
        Assert.Equal(100m, (await client.GetAccountAsync(account.Id)).Balance);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();

        var response = await client.PostAsJsonAsync($"/api/accounts/{account.Id}/deposits", new MoneyRequest(10m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_see_or_use_other_customers_accounts()
    {
        var owner = await factory.CreateCustomerClientAsync();
        var intruder = await factory.CreateCustomerClientAsync();
        var account = await owner.CreateAccountAsync();
        await owner.DepositAsync(account.Id, 100m);

        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"/api/accounts/{account.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.WithdrawAsync(account.Id, 50m)).StatusCode);
        Assert.Equal(100m, (await owner.GetAccountAsync(account.Id)).Balance);
    }

    [Fact]
    public async Task History_is_paginated_and_filterable_by_type()
    {
        var client = await factory.CreateCustomerClientAsync();
        var account = await client.CreateAccountAsync();
        for (var i = 1; i <= 5; i++)
            await client.DepositAsync(account.Id, i * 10m);
        await client.WithdrawAsync(account.Id, 5m);

        var page = await client.GetFromJsonAsync<PagedResponse<TransactionResponse>>(
            $"/api/accounts/{account.Id}/transactions?page=1&pageSize=4", BankingApiFactory.Json);
        var deposits = await client.GetFromJsonAsync<PagedResponse<TransactionResponse>>(
            $"/api/accounts/{account.Id}/transactions?type=Deposit", BankingApiFactory.Json);

        Assert.Equal(6, page!.TotalCount);
        Assert.Equal(4, page.Items.Count);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("Withdrawal", page.Items[0].Type);
        Assert.Equal(5, deposits!.TotalCount);
        Assert.All(deposits.Items, t => Assert.Equal("Deposit", t.Type));
    }
}
