namespace SmartCollectAPI.Services.Providers;

/// <summary>
/// Factory implementation for resolving embedding service providers.
/// Uses dependency injection to resolve providers registered in the DI container.
/// </summary>
public class EmbeddingProviderFactory : IEmbeddingProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmbeddingProviderFactory> _logger;

    // Map provider keys to service types
    private readonly Dictionary<string, Func<IEmbeddingService>> _providerResolvers;
    private readonly List<string> _availableProviders;

    public EmbeddingProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<EmbeddingProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Initialize provider resolvers
        _providerResolvers = new Dictionary<string, Func<IEmbeddingService>>(StringComparer.OrdinalIgnoreCase)
        {
            ["sentence-transformers"] = () => _serviceProvider.GetRequiredService<SentenceTransformerService>(),
            ["spacy"] = () => _serviceProvider.GetRequiredService<SpacyNlpService>(),
            // Future providers can be added here:
            // ["openai"] = () => _serviceProvider.GetRequiredService<OpenAIEmbeddingService>(),
            // ["cohere"] = () => _serviceProvider.GetRequiredService<CohereEmbeddingService>(),
        };

        _availableProviders = _providerResolvers.Keys.ToList();

        _logger.LogInformation(
            "EmbeddingProviderFactory initialized with {Count} providers: {Providers}",
            _availableProviders.Count,
            string.Join(", ", _availableProviders));
    }

    public IEmbeddingService GetProvider(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            _logger.LogWarning("Empty provider key requested, returning default provider");
            return GetDefaultProvider();
        }

        if (_providerResolvers.TryGetValue(providerKey, out var resolver))
        {
            try
            {
                var service = resolver();
                _logger.LogDebug("Resolved embedding provider: {ProviderKey}", providerKey);
                return service;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve provider {ProviderKey}, falling back to default", providerKey);
                return GetDefaultProvider();
            }
        }

        _logger.LogWarning(
            "Unknown provider key '{ProviderKey}', falling back to default. Available: {Available}",
            providerKey,
            string.Join(", ", _availableProviders));

        return GetDefaultProvider();
    }

    public IEmbeddingService GetDefaultProvider()
    {
        // sentence-transformers is our default: free, high quality, 768 dimensions
        return _serviceProvider.GetRequiredService<SentenceTransformerService>();
    }

    public IReadOnlyList<string> GetAvailableProviders()
    {
        return _availableProviders.AsReadOnly();
    }

    public bool IsProviderSupported(string providerKey)
    {
        return !string.IsNullOrWhiteSpace(providerKey) &&
               _providerResolvers.ContainsKey(providerKey);
    }
}
