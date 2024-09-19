using System.Collections.Concurrent;

using OpenAI_API;

namespace AutoGpt;

public class OpenAIClientFactory(IHttpClientFactory httpClientFactory)
{
    private readonly ConcurrentDictionary<string, Lazy<OpenAIAPI>> _clients = new();

    public OpenAIAPI CreateClient(string apiKey)
    {
        return _clients.GetOrAdd(apiKey, new Lazy<OpenAIAPI>(() =>
        {
            // 如果_clients大于100个，就移除第一个
            if (_clients.Count > 100)
            {
                _clients.TryRemove(_clients.Keys.First(), out _);
            }

            var client = new OpenAIAPI(apiKey) { HttpClientFactory = httpClientFactory };
            return client;
        })).Value;
    }
}