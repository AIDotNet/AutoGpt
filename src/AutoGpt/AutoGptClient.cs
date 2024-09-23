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
        string prompt, string apiKey, string model, int maxToken = 800, double? temperature = null)
    {
        var chat = new List<ChatMessage> { new ChatMessage(ChatMessageRole.User, prompt) };

        await foreach (var item in GenerateResponseAsync(chat, apiKey, model, maxToken, temperature))
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
        List<ChatMessage> chatMessages, string apiKey, string model, int maxToken = 800, double? temperature = null)
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

        // MrYSeven 20240923 这里看到暂时没使用 先注释
        // 计算一个合理的最大token
        //var makeMaxToken = maxToken / 2;

        //if (makeMaxToken < 800)
        //{
        //    makeMaxToken = 800;
        //}

        while (true)
        {
            startTime = DateTime.Now;
            var stepData = await MakeApiCall(chatHistory, maxToken, apiKey, model, temperature: temperature);
            endTime = DateTime.Now;
            var thinkingTime = (endTime - startTime).TotalSeconds;
            totalThinkingTime += thinkingTime;


            if (stepCount > options.Value.NumOutputs)
            {
                break;
            }

            if (stepData.Type == MakeResultDto.MakeResultType.Error)
            {
                steps.Add(new MakeResultDto($"Step {stepCount}: {stepData.Title}", stepData.Content, "error"));
                break;
            }

            steps.Add(new MakeResultDto(stepData.Title, stepData.Content, stepData.NextAction));

            chatHistory.Add(new ChatMessage(ChatMessageRole.Assistant,
                JsonSerializer.Serialize(stepData, _jsonSerializerOptions)));

            stepData.Title = $"Step {stepCount}: {stepData.Title}";

            stepData.Type = MakeResultDto.MakeResultType.Step;
            yield return stepData;


            if (stepData.NextAction?.Equals(MakeResultDto.FinalAnswerKey, StringComparison.OrdinalIgnoreCase) == true) // Max number of steps
            {
                break;
            }


            stepCount += 1;
        }

        startTime = DateTime.Now;

        endTime = DateTime.Now;
        var sb = new StringBuilder();

        var history = steps.Select(x => new ChatMessage(ChatMessageRole.Assistant, JsonSerializer.Serialize(x.Content, _jsonSerializerOptions))).ToList();

        history.Add(new ChatMessage(ChatMessageRole.User,
            "Please help the user give the best solution based on your reasoning above, if possible in as much detail as possible."));

        history.AddRange(chatMessages);

        await foreach (var content in MakeApiCallStreamAsync(history, maxToken, apiKey, model, temperature))
        {
            sb.Append(content);
            yield return new MakeResultDto("Final Answer", content, MakeResultDto.FinalAnswerKey,
                MakeResultDto.MakeResultType.FinalAnswer);
        }

        totalThinkingTime += (endTime - startTime).TotalSeconds;
    }

    private async IAsyncEnumerable<string> MakeApiCallStreamAsync(IList<ChatMessage> history, int maxToken, string apiKey,
        string model, double? temperature = null)
    {
        var openAiApi = clientFactory.CreateClient(apiKey);

        await foreach (var item in openAiApi.Chat.StreamChatEnumerableAsync(BuildChatRequest(history, maxToken, model, temperature)))
        {
            var content = item.Choices.FirstOrDefault()?.Delta.TextContent ?? string.Empty;

            yield return content;
        }
    }

    public async Task<MakeResultDto> MakeApiCall(List<ChatMessage> history, int maxToken, string apiKey,
        string model,
        bool isFinalAnswer = false, double? temperature = null /* 加在后面避免影响原有的顺序*/ )
    {
        var openAiApi = clientFactory.CreateClient(apiKey);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response =
                    await openAiApi.Chat.CreateChatCompletionAsync(BuildChatRequest(history, maxToken, model, temperature));

                var content = response?.Choices?.FirstOrDefault()?.Message.TextContent ?? string.Empty;

                var result = JsonSerializer.Deserialize<MakeResultDto>(content, _jsonSerializerOptions);

                return result;
            }
            catch (Exception e)
            {
                if (attempt == 2)
                {
                    if (isFinalAnswer)
                    {
                        return
                            new MakeResultDto("Error",
                                $"Failed to generate final answer after 3 attempts. Error: {e.Message}",
                                MakeResultDto.FinalAnswerKey, MakeResultDto.MakeResultType.Error);
                    }
                    else
                    {
                        return
                            new MakeResultDto("Error",
                                $"Failed to generate response after 3 attempts. Error: {e.Message}",
                                "continue", MakeResultDto.MakeResultType.Error);
                    }
                }

                await Task.Delay(1000);
            }
        }

        return new MakeResultDto("Error", "Failed to generate response after 3 attempts.", "continue",
            MakeResultDto.MakeResultType.Error);
    }

    #region 内部处理

    /// <summary>
    /// 构建ChatMessage
    /// </summary>
    /// <param name="history"></param>
    /// <param name="maxToken"></param>
    /// <param name="model"></param>
    /// <param name="temperature"></param>
    /// <returns></returns>
    private ChatRequest BuildChatRequest(IList<ChatMessage> history, int maxToken, string model, double? temperature = null)
    {
        if (!temperature.HasValue)
        {
            temperature = options.Value.Temperature;
        }

        return new ChatRequest()
        {
            Messages = history.ToArray(),
            MaxTokens = maxToken,
            Model = model,
            //json数组
            ResponseFormat = ChatRequest.ResponseFormats.JsonObject,
            Temperature = temperature,
            TopP = options.Value.TopP,
            NumChoicesPerMessage = options.Value.NumChoicesPerMessage,
            MultipleStopSequences = options.Value.MultipleStopSequences,
            FrequencyPenalty = options.Value.FrequencyPenalty,
            PresencePenalty = options.Value.PresencePenalty,
            LogitBias = options.Value.LogitBias
        };
    }

    #endregion 内部处理
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
        /// 错误
        /// </summary>
        Error,

        /// <summary>
        /// 最终答案
        /// </summary>
        FinalAnswer = 99
    }

    public static string FinalAnswerKey => "final_answer";
}