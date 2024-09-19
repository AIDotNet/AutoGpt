using System.Collections.Concurrent;

using OpenAI_API;

namespace AutoGpt;

/// <summary>
/// OpenAI API 客户端工厂
/// </summary>
/// <param name="httpClientFactory"></param>
public sealed class OpenAIClientFactory(IHttpClientFactory httpClientFactory)
{
    private readonly ConcurrentDictionary<string, Lazy<OpenAIAPI>> _clients = new();

    public OpenAIAPI CreateClient(string apiKey)
    {
        return _clients.GetOrAdd(apiKey, new Lazy<OpenAIAPI>(() =>
        {
            var client = new OpenAIAPI(apiKey) { HttpClientFactory = httpClientFactory };
            return client;
        })).Value;
    }
}