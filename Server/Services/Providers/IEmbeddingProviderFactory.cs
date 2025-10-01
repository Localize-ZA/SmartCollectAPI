namespace SmartCollectAPI.Services.Providers;

/// <summary>
/// Factory for resolving embedding service providers by key.
/// Enables dynamic selection of embedding providers based on document characteristics.
/// </summary>
public interface IEmbeddingProviderFactory
{
    /// <summary>
    /// Get an embedding service by provider key.
    /// </summary>
    /// <param name="providerKey">Provider identifier: "sentence-transformers", "spacy", "openai", "cohere"</param>
    /// <returns>The requested embedding service</returns>
    /// <exception cref="ArgumentException">If provider key is not supported</exception>
    IEmbeddingService GetProvider(string providerKey);

    /// <summary>
    /// Get the default embedding provider (sentence-transformers).
    /// </summary>
    /// <returns>The default embedding service</returns>
    IEmbeddingService GetDefaultProvider();

    /// <summary>
    /// Get all available provider keys.
    /// </summary>
    /// <returns>List of supported provider keys</returns>
    IReadOnlyList<string> GetAvailableProviders();

    /// <summary>
    /// Check if a provider key is supported.
    /// </summary>
    /// <param name="providerKey">Provider identifier to check</param>
    /// <returns>True if supported, false otherwise</returns>
    bool IsProviderSupported(string providerKey);
}
