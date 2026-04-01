using System.Text.Json;
using DI.MCP.Server.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace DI.MCP.Server.Services.Authentication;

/// <summary>
/// Validates tokens by mirroring the API-side AuthenticationFilter logic:
///   1. If IDP is configured, call the IDP userinfo endpoint with the Bearer token.
///   2. If IDP fails (or is disabled), fall back to Firebase token verification.
///   3. X-Internal-Key validation for service-to-service calls.
/// After validation succeeds the original token is passed as-is to the downstream API.
/// </summary>
public class TokenValidationService : ITokenValidationService
{
    private readonly AppSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(
        IOptions<AppSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenValidationService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<TokenValidationResult> ValidateBearerTokenAsync(string token)
    {
        // NOTE: The API-side Startup.cs also has UseOAuthBearerAuthentication / UseJwtBearerAuthentication
        // (OWIN middleware) that validates JWTs locally via signature check before the AuthenticationFilter.
        // We intentionally skip local JWT validation here because:
        //   1. Those are OWIN/.NET Framework APIs — not available in .NET 8.
        //   2. The IDP userinfo call below is more authoritative (catches revoked tokens).
        //   3. The downstream API still performs its own full validation pipeline.
        // If per-request latency from the userinfo call becomes a concern, consider adding
        // ASP.NET Core's AddJwtBearer() middleware as a fast-path optimisation.

        //IDP userinfo check
        if (_settings.IdpConsumeCentralTokens && !string.IsNullOrWhiteSpace(_settings.IdpAuthority))
        {
            var idpResult = await ValidateViaIdpAsync(token);
            if (idpResult.IsValid)
                return idpResult;

            _logger.LogDebug("IDP validation failed, falling back to Firebase");
        }

        //Firebase token verification
        if (!string.IsNullOrWhiteSpace(_settings.GoogleAuthKey))
        {
            var firebaseResult = await ValidateViaFirebaseAsync(token);
            if (firebaseResult.IsValid)
                return firebaseResult;
        }

        return TokenValidationResult.Failure("The token is not valid or has expired");
    }

    public TokenValidationResult ValidateInternalKey(string key)
    {
        if (string.IsNullOrWhiteSpace(_settings.InternalKey))
            return TokenValidationResult.Failure("Internal key authentication is not configured");

        // Case-sensitive comparison
        if (key == _settings.InternalKey)
        {
            _logger.LogDebug("X-Internal-Key validated for {User}", _settings.InternalAdminUsername);
            return TokenValidationResult.Success(_settings.InternalAdminUsername, "InternalKey");
        }

        return TokenValidationResult.Failure("Authorization is missing or invalid");
    }

    /// <summary>
    /// Validates the token against the IDP userinfo endpoint.
    /// Mirrors the API-side logic that calls {IdpAuthority}/connect/userinfo.
    /// </summary>
    private async Task<TokenValidationResult> ValidateViaIdpAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("IdpValidation");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var authority = _settings.IdpAuthority.TrimEnd('/');
            var response = await client.GetAsync($"{authority}/connect/userinfo");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("IDP userinfo returned {StatusCode}", response.StatusCode);
                return TokenValidationResult.Failure("IDP token validation failed");
            }

            var content = await response.Content.ReadAsStringAsync();
            var userData = JsonDocument.Parse(content).RootElement;

            var email = userData.TryGetProperty("email", out var e) ? e.GetString() : null;
            var superAdmin = userData.TryGetProperty("SuperAdmin", out var sa) ? sa.GetString() : null;
            var forceBlur = userData.TryGetProperty("ForceBlur", out var fb) ? fb.GetString() : null;
            var role = userData.TryGetProperty("role", out var r) ? r.GetString() : null;
            var clientId = userData.TryGetProperty("client_id", out var c) ? c.GetString() : null;

            // Validate required fields
            if (!string.IsNullOrWhiteSpace(email) &&
                !string.IsNullOrWhiteSpace(superAdmin) &&
                !string.IsNullOrWhiteSpace(forceBlur) &&
                !string.IsNullOrWhiteSpace(role) &&
                !string.IsNullOrWhiteSpace(clientId) &&
                clientId!.Equals(_settings.IdpExpectedClientId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("IDP token validated for {Email}", email);
                return TokenValidationResult.Success(email!, "IDP");
            }

            _logger.LogDebug("IDP userinfo response missing required fields or wrong client_id");
            return TokenValidationResult.Failure("IDP token validation failed: missing required claims");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IDP userinfo call failed");
            return TokenValidationResult.Failure("IDP token validation failed");
        }
    }

    /// <summary>
    /// Validates the token via Firebase Identity Toolkit (accounts:lookup).
    /// Mirrors the API-side FirebaseTokenVerifier logic.
    /// </summary>
    private async Task<TokenValidationResult> ValidateViaFirebaseAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("FirebaseValidation");
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={_settings.GoogleAuthKey}";

            var requestBody = JsonConvert.SerializeObject(new { idToken = token });
            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Firebase token verification returned {StatusCode}", response.StatusCode);
                return TokenValidationResult.Failure("Firebase token verification failed");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var verificationResponse = JsonConvert.DeserializeObject<FirebaseVerificationResponse>(responseString);

            if (verificationResponse?.Users == null || verificationResponse.Users.Count == 0)
                return TokenValidationResult.Failure("Firebase: no user found for token");

            var user = verificationResponse.Users[0];

            if (string.IsNullOrWhiteSpace(user.Email))
                return TokenValidationResult.Failure("Firebase: user has no email");

            _logger.LogDebug("Firebase token validated for {Email}", user.Email);
            return TokenValidationResult.Success(user.Email, "Firebase");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Firebase token verification failed");
            return TokenValidationResult.Failure("Firebase token verification failed");
        }
    }

    // ── Firebase response models (mirrors API-side models) ──

    private class FirebaseVerificationResponse
    {
        [JsonProperty("kind")]
        public string? Kind { get; set; }

        [JsonProperty("users")]
        public List<FirebaseUser>? Users { get; set; }
    }

    private class FirebaseUser
    {
        [JsonProperty("localId")]
        public string? LocalId { get; set; }

        [JsonProperty("email")]
        public string? Email { get; set; }

        [JsonProperty("emailVerified")]
        public bool EmailVerified { get; set; }

        [JsonProperty("customAttributes")]
        public string? CustomAttributes { get; set; }
    }
}
