using OpenAI.Chat;

namespace AutoGpt;

public interface IClientFactory
{
    ChatClient CreateClient(string model, string apiKey);
}