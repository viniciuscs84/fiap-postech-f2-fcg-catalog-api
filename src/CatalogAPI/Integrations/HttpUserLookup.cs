using System.Net;
using System.Net.Http.Headers;

namespace CatalogAPI.Integrations;

public sealed class HttpUserLookup(HttpClient client) : IUserLookup
{
    public async Task<UserLookupResult> FindAsync(Guid userId, string bearerToken, CancellationToken cancellationToken)
    {
        if (client.BaseAddress is null) return UserLookupResult.Unavailable;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/admin/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => UserLookupResult.Found,
                HttpStatusCode.NotFound => UserLookupResult.NotFound,
                _ => UserLookupResult.Unavailable
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (HttpRequestException) { return UserLookupResult.Unavailable; }
        catch (TaskCanceledException) { return UserLookupResult.Unavailable; }
    }
}
