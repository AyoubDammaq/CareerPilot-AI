namespace CareerPilot.API.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>The owner's login password (single-user app). Set via Auth__Password.</summary>
    public string Password { get; set; } = "";

    /// <summary>HMAC key used to sign tokens, at least 32 characters. Set via Auth__JwtKey.</summary>
    public string JwtKey { get; set; } = "";

    public string Issuer { get; set; } = "careerpilot";
    public string Audience { get; set; } = "careerpilot-web";
    public int TokenLifetimeMinutes { get; set; } = 480;

    private static bool LooksLikePlaceholder(string v) =>
        v.StartsWith("change-me", StringComparison.OrdinalIgnoreCase) || v.StartsWith("replace-with", StringComparison.OrdinalIgnoreCase);

    public void EnsureValid()
    {
        if (Password.Length < 12)
            throw new InvalidOperationException("Auth:Password must be set and at least 12 characters (env var Auth__Password).");
        if (LooksLikePlaceholder(Password))
            throw new InvalidOperationException("Auth:Password still has the example value from .env.example. Choose your own.");
        if (LooksLikePlaceholder(JwtKey))
            throw new InvalidOperationException("Auth:JwtKey still has the example value from .env.example. Generate one with: openssl rand -base64 48");
        if (JwtKey.Length < 32)
            throw new InvalidOperationException("Auth:JwtKey must be set and at least 32 characters (env var Auth__JwtKey).");
        if (TokenLifetimeMinutes is < 5 or > 24 * 60)
            throw new InvalidOperationException("Auth:TokenLifetimeMinutes must be between 5 and 1440.");
    }
}
