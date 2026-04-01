namespace DI.MCP.Server.Configuration;

public class AppSettings
{
    /// <summary>Base URL for the DI Analytics API (e.g. https://practice-users.dentalintel.com/api)</summary>
    public string ApiBaseUrl { get; set; } = "";

    /// <summary>Timeout in seconds for upstream DI API calls</summary>
    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>Enable debug mode (verbose errors)</summary>
    public bool Debug { get; set; } = false;

    /// <summary>Cache TTL in minutes for metric definitions (default: 1440 = 24 hours)</summary>
    public int CacheTtlMinutes { get; set; } = 1440;

    // ── Authentication settings (mirrors API-side AuthenticationFilter) ──

    /// <summary>IDP (IdentityServer) authority URL (e.g. https://idp.dentalintel.com/)</summary>
    public string IdpAuthority { get; set; } = "";

    /// <summary>Whether to validate tokens against the central IDP userinfo endpoint</summary>
    public bool IdpConsumeCentralTokens { get; set; } = false;

    /// <summary>Expected IDP client_id value for API tokens (e.g. "diapi")</summary>
    public string IdpExpectedClientId { get; set; } = "diapi";

    /// <summary>Google/Firebase API key for token verification</summary>
    public string GoogleAuthKey { get; set; } = "";

    /// <summary>Shared secret for X-Internal-Key header authentication (case-sensitive)</summary>
    public string InternalKey { get; set; } = "";

    /// <summary>Username assigned to requests authenticated via X-Internal-Key</summary>
    public string InternalAdminUsername { get; set; } = "internal-admin@dentalintel.com";
}
