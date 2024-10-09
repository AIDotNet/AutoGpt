using System.ClientModel;
using System.Collections.Concurrent;

using AutoGpt.Options;

using Azure.AI.OpenAI;

using Microsoft.Extensions.Options;

using OpenAI.Chat;

namespace AutoGpt;

public class AzureOpenAIClientFactory(IOptions<AutoGptOptions> options) : IClientFactory
{
    private readonly ConcurrentDictionary<string, Lazy<ChatClient>> _clients = new();
    private readonly ConcurrentDictionary<string, Lazy<AzureOpenAIClient>> _azureClients = new();

    private readonly string _endpoint = options.Value.Endpoint.TrimEnd('/');

    public ChatClient CreateClient(string model, string apiKey)
    {
        return _clients.GetOrAdd(model + apiKey, new Lazy<ChatClient>(() =>
        {
            var azureClient = GetAzureClient(apiKey);

            return azureClient.GetChatClient(model);
        })).Value;
    }

    private AzureOpenAIClient GetAzureClient(string apiKey)
    {
        return _azureClients.GetOrAdd(apiKey,
                new Lazy<AzureOpenAIClient>(() =>
                    new AzureOpenAIClient(new Uri(_endpoint), new ApiKeyCredential(apiKey))))
            .Value;
    }
}