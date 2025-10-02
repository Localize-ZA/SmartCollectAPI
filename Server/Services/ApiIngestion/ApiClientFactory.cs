using SmartCollectAPI.Models;

namespace SmartCollectAPI.Services.ApiIngestion;

/// <summary>
/// Factory for creating appropriate API client based on source type
/// </summary>
public interface IApiClientFactory
{
    /// <summary>
    /// Get the appropriate API client for the given source
    /// </summary>
    IApiClient GetClient(ApiSource source);
}

/// <summary>
/// Implementation of API client factory
/// </summary>
public class ApiClientFactory : IApiClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApiClientFactory> _logger;

    public ApiClientFactory(IServiceProvider serviceProvider, ILogger<ApiClientFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get the appropriate API client based on source API type
    /// </summary>
    public IApiClient GetClient(ApiSource source)
    {
        var apiType = source.ApiType?.ToUpperInvariant() ?? "REST";

        _logger.LogDebug("Creating API client for type: {ApiType}", apiType);

        return apiType switch
        {
            "REST" => _serviceProvider.GetRequiredService<RestApiClient>(),
            "GRAPHQL" => _serviceProvider.GetRequiredService<GraphQLClient>(),
            _ => throw new NotSupportedException($"API type '{apiType}' is not supported")
        };
    }
}
