using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SmartCollectAPI.Models;
using SmartCollectAPI.Services;
using SmartCollectAPI.Data;

namespace SmartCollectAPI.Services.ApiIngestion;

public interface IAuthenticationManager
{
    Task ApplyAuthenticationAsync(HttpRequestMessage request, ApiSource source, CancellationToken cancellationToken = default);
    string EncryptCredentials(Dictionary<string, string> credentials);
    Dictionary<string, string> DecryptCredentials(string encryptedCredentials);
}

public class AuthenticationManager : IAuthenticationManager
{
    private readonly IDataProtector _protector;
    private readonly ILogger<AuthenticationManager> _logger;
    private readonly ISecretCryptoService _crypto;
    private readonly SmartCollectDbContext _db;

    public AuthenticationManager(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AuthenticationManager> logger,
        ISecretCryptoService crypto,
        SmartCollectDbContext db)
    {
        _protector = dataProtectionProvider.CreateProtector("ApiIngestion.AuthConfig");
        _logger = logger;
        _crypto = crypto;
        _db = db;
    }

    public async Task ApplyAuthenticationAsync(
        HttpRequestMessage request, 
        ApiSource source, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(source.AuthType) || source.AuthType.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrEmpty(source.AuthConfigEncrypted))
        {
            _logger.LogWarning("Auth type {AuthType} specified but no auth config provided for source {SourceName}", 
                source.AuthType, source.Name);
            return;
        }

        try
        {
            var authConfig = DecryptCredentials(source.AuthConfigEncrypted);

            switch (source.AuthType.ToUpperInvariant())
            {
                case "BASIC":
                    ApplyBasicAuth(request, authConfig);
                    break;

                case "BEARER":
                    ApplyBearerAuth(request, authConfig);
                    break;

                case "APIKEY":
                    await ApplyApiKeyAuthAsync(request, source, authConfig, cancellationToken);
                    break;

                case "OAUTH2":
                    await ApplyOAuth2Async(request, authConfig, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown auth type: {AuthType}", source.AuthType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply authentication for source {SourceName}", source.Name);
            throw;
        }
    }

    public string EncryptCredentials(Dictionary<string, string> credentials)
    {
        var json = JsonSerializer.Serialize(credentials);
        return _protector.Protect(json);
    }

    public Dictionary<string, string> DecryptCredentials(string encryptedCredentials)
    {
        var json = _protector.Unprotect(encryptedCredentials);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) 
            ?? new Dictionary<string, string>();
    }

    private void ApplyBasicAuth(HttpRequestMessage request, Dictionary<string, string> authConfig)
    {
        if (!authConfig.TryGetValue("username", out var username) ||
            !authConfig.TryGetValue("password", out var password))
        {
            throw new InvalidOperationException("Basic auth requires 'username' and 'password' in auth config");
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        _logger.LogDebug("Applied Basic authentication for user {Username}", username);
    }

    private void ApplyBearerAuth(HttpRequestMessage request, Dictionary<string, string> authConfig)
    {
        if (!authConfig.TryGetValue("token", out var token))
        {
            throw new InvalidOperationException("Bearer auth requires 'token' in auth config");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _logger.LogDebug("Applied Bearer token authentication");
    }

    private async Task ApplyApiKeyAuthAsync(HttpRequestMessage request, ApiSource source, Dictionary<string, string> legacyAuthConfig, CancellationToken cancellationToken)
    {
        string? apiKey = null;
        string location;
        string? headerName = null;
        string? queryParam = null;

        // Prefer structured encrypted fields
        if (source.HasApiKey && source.ApiKeyCiphertext != null && source.ApiKeyIv != null && source.ApiKeyTag != null && source.KeyVersion.HasValue)
        {
            apiKey = _crypto.Decrypt(source.ApiKeyCiphertext, source.ApiKeyIv, source.ApiKeyTag, source.KeyVersion.Value);
            location = string.IsNullOrWhiteSpace(source.AuthLocation) ? "header" : source.AuthLocation!;
            headerName = string.IsNullOrWhiteSpace(source.HeaderName) ? "X-API-Key" : source.HeaderName;
            queryParam = string.IsNullOrWhiteSpace(source.QueryParam) ? "api_key" : source.QueryParam;
        }
        else
        {
            // Fallback to legacy encrypted JSON config if present
            if (!legacyAuthConfig.TryGetValue("key", out var legacyKey))
            {
                throw new InvalidOperationException("API Key auth requires a configured key");
            }
            apiKey = legacyKey;
            location = legacyAuthConfig.TryGetValue("in", out var loc) ? loc : "header";
            headerName = legacyAuthConfig.GetValueOrDefault("header", "X-API-Key");
            queryParam = legacyAuthConfig.GetValueOrDefault("param", "api_key");
        }

        // Apply without logging sensitive values
        if (string.Equals(location, "query", StringComparison.OrdinalIgnoreCase))
        {
            var paramName = queryParam ?? "api_key";
            var uriBuilder = new UriBuilder(request.RequestUri!);
            var query = string.IsNullOrEmpty(uriBuilder.Query)
                ? $"{paramName}={Uri.EscapeDataString(apiKey!)}"
                : $"{uriBuilder.Query.TrimStart('?')}&{paramName}={Uri.EscapeDataString(apiKey!)}";
            uriBuilder.Query = query;
            request.RequestUri = uriBuilder.Uri;
        }
        else
        {
            var h = headerName ?? "X-API-Key";
            request.Headers.TryAddWithoutValidation(h, apiKey);
        }

        // Try update last_used_at without throwing on failure
        try
        {
            source.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch { /* best-effort */ }
    }

    private async Task ApplyOAuth2Async(
        HttpRequestMessage request, 
        Dictionary<string, string> authConfig, 
        CancellationToken cancellationToken)
    {
        // For now, OAuth2 requires a pre-obtained access token
        // Future enhancement: Implement token refresh flow
        if (!authConfig.TryGetValue("access_token", out var accessToken))
        {
            throw new InvalidOperationException("OAuth2 auth requires 'access_token' in auth config");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        _logger.LogDebug("Applied OAuth2 authentication");

        await Task.CompletedTask; // For async consistency
    }
}
