using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using AutoGpt.Options;

using Microsoft.Extensions.Options;

using OpenAI.Chat;

#pragma warning disable SKEXP0010

namespace AutoGpt;

public class AutoGptClient(
    IClientFactory clientFactory,
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
        string prompt, string apiKey, string model, int maxToken = 800, float? temperature = null)
    {
        var chat = new List<ChatMessage> { ChatMessage.CreateUserMessage(prompt) };

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
    /// <param name="temperature"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<MakeResultDto> GenerateResponseAsync(
        List<ChatMessage> chatMessages, string apiKey, string model, int maxToken = 800, float? temperature = null)
    {
        var chatHistory = new List<ChatMessage>();
        foreach (var item in promptManager.Prompts)
        {
            switch (item.Key)
            {
                case "user":
                    chatHistory.Add(ChatMessage.CreateUserMessage(item.Value));
                    break;
                case "assistant":
                    chatHistory.Add(ChatMessage.CreateAssistantMessage(item.Value));
                    break;
                case "system":
                    chatHistory.Add(ChatMessage.CreateSystemMessage(item.Value));
                    break;
                case "tool":
                    chatHistory.Add(ChatMessage.CreateToolMessage(item.Value));
                    break;
            }
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
            var stepData = await MakeApiCall(chatHistory, maxToken, apiKey, model);
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

            chatHistory.Add(ChatMessage.CreateAssistantMessage(
                JsonSerializer.Serialize(stepData, _jsonSerializerOptions)));

            stepData.Title = $"Step {stepCount}: {stepData.Title}";

            stepData.Type = MakeResultDto.MakeResultType.Step;
            yield return stepData;


            if (stepData.NextAction?.Equals(MakeResultDto.FinalAnswerKey, StringComparison.OrdinalIgnoreCase) ==
                true) // Max number of steps
            {
                break;
            }


            stepCount += 1;
        }

        startTime = DateTime.Now;

        endTime = DateTime.Now;
        var sb = new StringBuilder();

        var history = steps.Select(x =>
                (ChatMessage)ChatMessage.CreateAssistantMessage(JsonSerializer.Serialize(x.Content,
                    _jsonSerializerOptions)))
            .ToList();

        history.Add(ChatMessage.CreateUserMessage(
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

    private async IAsyncEnumerable<string> MakeApiCallStreamAsync(IList<ChatMessage> history, int maxToken,
        string apiKey,
        string model, float? temperature = null)
    {
        var openAiApi = clientFactory.CreateClient(model, apiKey);

        await foreach (var streamingChatUpdate in openAiApi.CompleteChatStreamingAsync(history,
                           new ChatCompletionOptions()
                           {
                               Temperature = temperature,
                               MaxOutputTokenCount = maxToken,
                               TopP = options.Value.TopP,
                               LogitBiases = { options.Value.LogitBias },
                               FrequencyPenalty = options.Value.FrequencyPenalty,
                               PresencePenalty = options.Value.PresencePenalty,
                           }))
        {
            foreach (ChatMessageContentPart contentPart in streamingChatUpdate.ContentUpdate)
            {
                yield return contentPart.Text;
            }
        }
    }

    public async Task<MakeResultDto> MakeApiCall(List<ChatMessage> history, int maxToken, string apiKey,
        string model,
        bool isFinalAnswer = false, float? temperature = null /* 加在后面避免影响原有的顺序*/)
    {
        var openAiApi = clientFactory.CreateClient(model, apiKey);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response =
                    await openAiApi.CompleteChatAsync(history,
                        new ChatCompletionOptions()
                        {
                            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                            Temperature = temperature,
                            MaxOutputTokenCount = maxToken,
                            TopP = options.Value.TopP,
                            LogitBiases = { options.Value.LogitBias },
                            FrequencyPenalty = options.Value.FrequencyPenalty,
                            PresencePenalty = options.Value.PresencePenalty,
                        });

                var content = response.Value.Content.FirstOrDefault()?.Text;

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