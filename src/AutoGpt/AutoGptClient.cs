using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using AutoGpt.Options;

using Microsoft.Extensions.Options;

using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Models;

#pragma warning disable SKEXP0010

namespace AutoGpt;

public class AutoGptClient(
    OpenAIClientFactory clientFactory,
    IOptions<AutoGptOptions> options,
    IPromptManager promptManager)
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generate response based on the prompt.
    /// </summary>
    /// <param name="prompt"></param>
    /// <param name="apiKey"></param>
    /// <param name="model"></param>
    /// <param name="maxToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<MakeResultDto> GenerateResponseAsync(
        string prompt, string apiKey, string model, int maxToken = 800)
    {
        var chat = new List<ChatMessage> { new ChatMessage(ChatMessageRole.User, prompt) };

        await foreach (var item in GenerateResponseAsync(chat, apiKey, model, maxToken))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Generate response based on the chat history.
    /// </summary>
    /// <param name="model"></param>
    /// <param name="maxToken"></param>
    /// <param name="chatMessages"></param>
    /// <param name="apiKey"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<MakeResultDto> GenerateResponseAsync(
        List<ChatMessage> chatMessages, string apiKey, string model, int maxToken = 800)
    {
        var chatHistory = new List<ChatMessage>();
        foreach (var item in promptManager.Prompts)
        {
            chatHistory.Add(new ChatMessage(ChatMessageRole.FromString(item.Key), item.Value));
        }

        chatHistory.AddRange(chatMessages);

        var totalThinkingTime = 0.0;
        var steps = new List<MakeResultDto>();
        var stepCount = 1;
        DateTime startTime;
        DateTime endTime;

        // 计算一个合理的最大tokn
        var makeMaxToken = maxToken / 2;

        if (makeMaxToken < 800)
        {
            makeMaxToken = 800;
        }

        while (true)
        {
            startTime = DateTime.Now;
            var stepData = await MakeApiCall(chatHistory, maxToken, apiKey, model);
            endTime = DateTime.Now;
            var thinkingTime = (endTime - startTime).TotalSeconds;
            totalThinkingTime += thinkingTime;

            steps.AddRange(stepData);

            foreach (MakeResultDto step in steps)
            {
                step.Type = MakeResultDto.MakeResultType.Step;
                yield return step;
            }

            if (stepData.Any(x => x.NextAction == "final_answer") ||
                stepCount > options.Value.NumOutputs) // Max number of steps
            {
                break;
            }

            chatHistory.Add(new ChatMessage(ChatMessageRole.Assistant,
                JsonSerializer.Serialize(stepData, _jsonSerializerOptions)));

            stepCount += stepData.Count;
        }

        startTime = DateTime.Now;

        endTime = DateTime.Now;
        var sb = new StringBuilder();

        var history = steps.Select(x => new ChatMessage(ChatMessageRole.Assistant, x.Content)).ToList();

        history.Add(new ChatMessage(ChatMessageRole.User,
            "Please help the user give the best solution based on your reasoning above, if possible in as much detail as possible."));

        history.AddRange(chatMessages);

        await foreach (var content in MakeApiCallStreamAsync(history.ToArray(), maxToken, apiKey, model))
        {
            sb.Append(content);
            yield return new MakeResultDto("Final Answer", content, "final_answer",
                MakeResultDto.MakeResultType.FinalAnswer);
        }

        totalThinkingTime += (endTime - startTime).TotalSeconds;
    }

    private async IAsyncEnumerable<string> MakeApiCallStreamAsync(ChatMessage[] history, int maxToken, string apiKey,
        string model)
    {
        var openAiApi = clientFactory.CreateClient(apiKey);

        await foreach (var item in openAiApi.Chat.StreamChatEnumerableAsync(new ChatRequest()
                       {
                           Messages = history, MaxTokens = maxToken, Model = model, Temperature = 0.2
                       }))
        {
            var content = item.Choices.FirstOrDefault()?.Message.TextContent ?? string.Empty;

            yield return content;
        }
    }

    public async Task<List<MakeResultDto>> MakeApiCall(List<ChatMessage> history, int maxToken, string apiKey,
        string model,
        bool isFinalAnswer = false)
    {
        var openAiApi = clientFactory.CreateClient(apiKey);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response =
                    await openAiApi.Chat.CreateChatCompletionAsync(new ChatRequest()
                    {
                        Messages = history.ToArray(),
                        MaxTokens = maxToken,
                        Model = model,
                        //json数组
                        Temperature = 0.2
                    });
                
                var content = response?.Choices?.FirstOrDefault()?.Message.TextContent ?? string.Empty;

                var result = JsonSerializer.Deserialize<List<MakeResultDto>>(content, _jsonSerializerOptions);

                return result;
            }
            catch (Exception e)
            {
                if (attempt == 2)
                {
                    if (isFinalAnswer)
                    {
                        return new List<MakeResultDto>
                        {
                            new("Error",
                                $"Failed to generate final answer after 3 attempts. Error: {e.Message}",
                                "final_answer")
                        };
                    }
                    else
                    {
                        return new List<MakeResultDto>
                        {
                            new("Error",
                                $"Failed to generate response after 3 attempts. Error: {e.Message}",
                                "continue")
                        };
                    }
                }

                await Task.Delay(1000);
            }
        }

        return new List<MakeResultDto>();
    }
}

public class MakeResultDto
{
    public MakeResultDto()
    {
    }

    public MakeResultDto(string title, string content, string nextAction, MakeResultType type = MakeResultType.Step)
    {
        Title = title;
        Content = content;
        NextAction = nextAction;
        Type = type;
    }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("content")] public string? Content { get; set; }

    [JsonPropertyName("next_action")] public string? NextAction { get; set; }

    [JsonPropertyName("type")] public MakeResultType? Type { get; set; }

    public enum MakeResultType
    {
        /// <summary>
        /// 推理步骤
        /// </summary>
        Step = 1,

        /// <summary>
        /// 继续
        /// </summary>
        Continue,

        /// <summary>
        /// 最终答案
        /// </summary>
        FinalAnswer = 99
    }
}