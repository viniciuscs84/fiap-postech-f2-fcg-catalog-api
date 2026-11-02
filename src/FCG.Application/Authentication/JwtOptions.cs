namespace FCG.Application.Authentication;

/// <summary>Configuration used to issue and validate JWT tokens.</summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Token issuer.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Token audience.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric signing key.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Token lifetime in minutes.</summary>
    public int ExpirationMinutes { get; init; } = 60;
}
