namespace DI.MCP.Server.Services.Authentication;

/// <summary>
/// Validates incoming authentication tokens (Bearer via IDP/Firebase, or X-Internal-Key)
/// mirroring the API-side AuthenticationFilter logic.
/// </summary>
public interface ITokenValidationService
{
    /// <summary>
    /// Validates the given Bearer token using IDP userinfo and/or Firebase verification.
    /// Returns a <see cref="TokenValidationResult"/> indicating success/failure.
    /// </summary>
    Task<TokenValidationResult> ValidateBearerTokenAsync(string token);

    /// <summary>
    /// Validates the X-Internal-Key header value against the configured secret.
    /// </summary>
    TokenValidationResult ValidateInternalKey(string key);
}

/// <summary>
/// Result of a token validation attempt.
/// </summary>
public class TokenValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? UserEmail { get; init; }
    public string? AuthMethod { get; init; }

    public static TokenValidationResult Success(string userEmail, string authMethod) =>
        new() { IsValid = true, UserEmail = userEmail, AuthMethod = authMethod };

    public static TokenValidationResult Failure(string error) =>
        new() { IsValid = false, Error = error };
}
