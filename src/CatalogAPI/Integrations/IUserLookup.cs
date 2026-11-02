namespace CatalogAPI.Integrations;

public enum UserLookupResult { Found, NotFound, Unavailable }

public interface IUserLookup
{
    Task<UserLookupResult> FindAsync(Guid userId, string bearerToken, CancellationToken cancellationToken);
}
