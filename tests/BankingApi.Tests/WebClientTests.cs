using System.Net;

namespace BankingApi.Tests;

public class WebClientTests(BankingApiFactory factory) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Root_serves_the_web_client()
    {
        var response = await factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("js/app.js", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/js/app.js", "text/javascript")]
    [InlineData("/css/styles.css", "text/css")]
    public async Task Static_assets_are_served(string path, string mediaType)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }
}
