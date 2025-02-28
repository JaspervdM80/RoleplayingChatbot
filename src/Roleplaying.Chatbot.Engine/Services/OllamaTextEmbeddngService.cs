using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

namespace Roleplaying.Chatbot.Engine.Services;

/// <summary>
/// Implementation of ITextEmbeddingGenerationService for Ollama
/// </summary>
public class OllamaTextEmbeddingGeneration : ITextEmbeddingGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;
    private readonly string _endpoint;
    private readonly bool _normalize;
    private readonly Dictionary<string, object?> _attributes;

    /// <summary>
    /// Creates a new instance of OllamaTextEmbeddingGeneration
    /// </summary>
    public OllamaTextEmbeddingGeneration(
        string endpoint,
        string modelId,
        int dimensions,
        bool normalize = false,
        Dictionary<string, object?>? defaultOptions = null)
    {
        _httpClient = new HttpClient();
        _endpoint = endpoint.TrimEnd('/');
        _modelId = modelId;
        _normalize = normalize;

        // Initialize the required attributes
        _attributes = defaultOptions ?? [];

        _attributes["ModelId"] = modelId;
        _attributes["Dimensions"] = dimensions;
        _attributes["Endpoint"] = endpoint;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Attributes => _attributes;

    /// <inheritdoc/>
    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> texts,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var embeddingsList = new List<ReadOnlyMemory<float>>();

        foreach (var text in texts)
        {
            // Format the request as Ollama expects
            var requestBody = new OllamaEmbeddingRequest
            {
                Model = _modelId,
                Prompt = text
            };

            // Add any options if configured
            if (_attributes.TryGetValue("Options", out var options) &&
                options is Dictionary<string, object> optionsDict)
            {
                requestBody.Options = optionsDict;
            }

            // Send the request to Ollama
            var response = await _httpClient.PostAsJsonAsync(
                $"{_endpoint}/api/embeddings",
                requestBody,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Parse the response
            var ollamaResponse = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                cancellationToken: cancellationToken);

            if (ollamaResponse == null || ollamaResponse.Embedding == null || ollamaResponse.Embedding.Length == 0)
            {
                throw new InvalidOperationException("Failed to get valid embeddings from Ollama");
            }

            // Apply normalization if configured
            var embedding = ollamaResponse.Embedding;
            if (_normalize)
            {
                embedding = NormalizeEmbedding(embedding);
            }

            // Convert to ReadOnlyMemory<float>
            embeddingsList.Add(new ReadOnlyMemory<float>(embedding));
        }

        return embeddingsList;
    }

    /// <summary>
    /// Creates a new embedding service with the specified attribute added
    /// </summary>
    public ITextEmbeddingGenerationService WithAttribute(string key, object? value)
    {
        _attributes[key] = value;
        return this;
    }

    private static float[] NormalizeEmbedding(float[] embedding)
    {
        // Calculate L2 norm (Euclidean length)
        var sumSquares = 0.0f;
        foreach (var value in embedding)
        {
            sumSquares += value * value;
        }
        var norm = (float)Math.Sqrt(sumSquares);

        // Prevent division by zero
        if (norm < 1e-6f)
        {
            return embedding;
        }

        // Normalize
        var normalized = new float[embedding.Length];
        for (var i = 0; i < embedding.Length; i++)
        {
            normalized[i] = embedding[i] / norm;
        }

        return normalized;
    }

    private class OllamaEmbeddingRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; }

        [JsonPropertyName("options")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Options { get; set; }

        public OllamaEmbeddingRequest()
        {
            Model = string.Empty;
            Prompt = string.Empty;
        }
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
