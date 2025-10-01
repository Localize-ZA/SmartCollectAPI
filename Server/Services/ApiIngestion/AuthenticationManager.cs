using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using SmartCollectAPI.Models;

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

    public AuthenticationManager(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AuthenticationManager> logger)
    {
        _protector = dataProtectionProvider.CreateProtector("ApiIngestion.AuthConfig");
        _logger = logger;
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
                    ApplyApiKeyAuth(request, authConfig);
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

    private void ApplyApiKeyAuth(HttpRequestMessage request, Dictionary<string, string> authConfig)
    {
        if (!authConfig.TryGetValue("key", out var apiKey))
        {
            throw new InvalidOperationException("API Key auth requires 'key' in auth config");
        }

        // Get header name (default to X-API-Key)
        var headerName = authConfig.GetValueOrDefault("header", "X-API-Key");

        // Check if it should be in query parameter instead
        if (authConfig.TryGetValue("in", out var location) && 
            location.Equals("query", StringComparison.OrdinalIgnoreCase))
        {
            var paramName = authConfig.GetValueOrDefault("param", "api_key");
            var uriBuilder = new UriBuilder(request.RequestUri!);
            var query = string.IsNullOrEmpty(uriBuilder.Query)
                ? $"{paramName}={Uri.EscapeDataString(apiKey)}"
                : $"{uriBuilder.Query.TrimStart('?')}&{paramName}={Uri.EscapeDataString(apiKey)}";
            uriBuilder.Query = query;
            request.RequestUri = uriBuilder.Uri;

            _logger.LogDebug("Applied API Key authentication in query parameter {ParamName}", paramName);
        }
        else
        {
            request.Headers.TryAddWithoutValidation(headerName, apiKey);
            _logger.LogDebug("Applied API Key authentication in header {HeaderName}", headerName);
        }
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
