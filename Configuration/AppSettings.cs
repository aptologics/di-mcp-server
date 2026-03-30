namespace DIMCPServer.Configuration;

public class AppSettings
{
    /// <summary>Base URL for the DI Analytics API (e.g. https://practice-users.dentalintel.com/api)</summary>
    public string ApiBaseUrl { get; set; } = "";

    /// <summary>Timeout in seconds for upstream DI API calls</summary>
    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>Enable debug mode (verbose errors)</summary>
    public bool Debug { get; set; } = false;
}
