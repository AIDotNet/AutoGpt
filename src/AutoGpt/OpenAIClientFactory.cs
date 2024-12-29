using System.ClientModel;
using System.Collections.Concurrent;

using AutoGpt.Options;

using Microsoft.Extensions.Options;

using OpenAI;
using OpenAI.Chat;

namespace AutoGpt;

/// <summary>
/// OpenAI API 客户端工厂
/// </summary>
public sealed class OpenAiClientFactory(IOptions<AutoGptOptions> options) : IClientFactory
{
    private readonly ConcurrentDictionary<string, Lazy<ChatClient>> _clients = new();

    private readonly string _endpoint = options.Value.Endpoint.TrimEnd('/') + "/v1";

    public ChatClient CreateClient(string model, string apiKey)
    {
        return _clients.GetOrAdd(model + apiKey, new Lazy<ChatClient>(() =>
        {
            var client = new ChatClient(model: model, new ApiKeyCredential(apiKey),
                new OpenAIClientOptions() { Endpoint = new Uri(_endpoint) ,OrganizationId = "auto-gpt"});
            return client;
        })).Value;
    }
}