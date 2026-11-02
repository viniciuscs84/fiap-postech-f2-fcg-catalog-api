using System.Net;
using CatalogAPI.Integrations;

namespace CatalogAPI.Tests.Purchases;

public sealed class UserLookupTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, UserLookupResult.Found)]
    [InlineData(HttpStatusCode.NotFound, UserLookupResult.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, UserLookupResult.Unavailable)]
    public async Task Lookup_uses_configured_UsersAPI_and_forwards_only_the_admin_token(
        HttpStatusCode status, UserLookupResult expected)
    {
        var handler = new RecordingHandler(status);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://usersapi:8080/") };
        var lookup = new HttpUserLookup(client);
        var target = Guid.NewGuid();

        var result = await lookup.FindAsync(target, "admin-jwt", CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal($"http://usersapi:8080/api/admin/users/{target}", handler.RequestUri);
        Assert.Equal("Bearer admin-jwt", handler.Authorization);
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
