namespace CareerPilot.API;

/// <summary>Hardening settings. Every value can be overridden with an environment variable such as Security__LoginPerMinute.</summary>
public sealed class SecurityOptions
{
    public const string Section = "Security";

    /// <summary>Failed or successful sign-in attempts allowed per client address per minute.</summary>
    public int LoginPerMinute { get; set; } = 5;

    /// <summary>Requests allowed per client address per minute for the whole API (a ceiling against runaway scripts).</summary>
    public int GlobalPerMinute { get; set; } = 600;

    /// <summary>Requests per minute to the endpoints that call an AI provider, search job boards or parse uploads.</summary>
    public int ExpensivePerMinute { get; set; } = 20;

    /// <summary>Largest request body accepted anywhere, in megabytes (the CV upload itself is limited to 5 MB).</summary>
    public int MaxRequestBodyMb { get; set; } = 7;

    /// <summary>
    /// Trust X-Forwarded-For / X-Forwarded-Proto from the reverse proxy in front of the API. Turn on ONLY when every request
    /// reaches the API through your own proxy (the supplied docker-compose setup does this); otherwise a caller could forge its address.
    /// </summary>
    public bool TrustForwardedHeaders { get; set; }

    /// <summary>How many proxies sit in front of the API. 1 for the supplied nginx; 2 if you also put your own TLS proxy in front of nginx.</summary>
    public int ForwardLimit { get; set; } = 1;

    public void EnsureValid()
    {
        if (LoginPerMinute is < 1 or > 1000) throw new InvalidOperationException("Security:LoginPerMinute must be between 1 and 1000.");
        if (GlobalPerMinute is < 10 or > 100000) throw new InvalidOperationException("Security:GlobalPerMinute must be between 10 and 100000.");
        if (ExpensivePerMinute is < 1 or > 10000) throw new InvalidOperationException("Security:ExpensivePerMinute must be between 1 and 10000.");
        if (MaxRequestBodyMb is < 1 or > 50) throw new InvalidOperationException("Security:MaxRequestBodyMb must be between 1 and 50.");
        if (ForwardLimit is < 1 or > 5) throw new InvalidOperationException("Security:ForwardLimit must be between 1 and 5.");
    }
}
