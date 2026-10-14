using System.Net;
using System.Net.Http.Json;
using BankingApi.Api.Contracts;

namespace BankingApi.Tests;

public class AdminTests(BankingApiFactory factory) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Frozen_accounts_reject_money_movements_until_unfrozen()
    {
        var admin = await factory.CreateAdminClientAsync();
        var customer = await factory.CreateCustomerClientAsync();
        var account = await customer.CreateAccountAsync();

        var freeze = await admin.PostAsync($"/api/admin/accounts/{account.Id}/freeze", null);
        Assert.Equal(HttpStatusCode.OK, freeze.StatusCode);

        var blocked = await customer.DepositAsync(account.Id, 100m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        Assert.Equal("account_frozen", await blocked.ErrorCodeAsync());

        await admin.PostAsync($"/api/admin/accounts/{account.Id}/unfreeze", null);
        Assert.Equal(HttpStatusCode.Created, (await customer.DepositAsync(account.Id, 100m)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_all_accounts()
    {
        var admin = await factory.CreateAdminClientAsync();
        var customer = await factory.CreateCustomerClientAsync();
        await customer.CreateAccountAsync();

        var result = await admin.GetFromJsonAsync<PagedResponse<AccountResponse>>("/api/admin/accounts", BankingApiFactory.Json);

        Assert.True(result!.TotalCount >= 1);
    }

    [Fact]
    public async Task Customers_cannot_use_admin_endpoints()
    {
        var customer = await factory.CreateCustomerClientAsync();

        var response = await customer.GetAsync("/api/admin/accounts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
