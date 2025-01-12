using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using AutoGpt.Options;

using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

using OpenAI.Chat;

using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

#pragma warning disable SKEXP0010

namespace AutoGpt;

public class AutoGptClient(
    IOptions<AutoGptOptions> options,
    Kernel kernel,
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
    /// <param name="maxToken"></param>
    /// <param name="temperature"></param>
    /// <param name="promptHandler">提示词处理回调</param>
    /// <param name="clientKernel"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<MakeResultDto> GenerateResponseAsync(
        string prompt, int maxToken = 800, float? temperature = null,
        Action<Dictionary<string, string>>? promptHandler = null, Kernel? clientKernel = null)
    {
        var chat = new List<ChatMessageContent>
        {
            new() { Content = prompt, Encoding = Encoding.UTF8, Role = AuthorRole.User }
        };

        await foreach (var item in GenerateResponseAsync(chat, maxToken, temperature, promptHandler, clientKernel))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Generate response based on the chat history.
    /// </summary>
    /// <param name="maxToken"></param>
    /// <param name="chatMessages"></param>
    /// <param name="temperature"></param>
    /// <param name="promptHandler"></param>
    /// <param name="clientKernel"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<MakeResultDto> GenerateResponseAsync(
        List<ChatMessageContent> chatMessages, int maxToken = 800,
        float? temperature = null,
        Action<Dictionary<string, string>>? promptHandler = null,
        Kernel? clientKernel = null)
    {
        var chatHistory = new ChatHistory();

        promptHandler?.Invoke(promptManager.Prompts);

        foreach (var item in promptManager.Prompts)
        {
            switch (item.Key)
            {
                case "user":
                    chatHistory.AddUserMessage(item.Value);
                    break;
                case "assistant":
                    chatHistory.AddAssistantMessage(item.Value);
                    break;
                case "system":
                    chatHistory.AddSystemMessage(item.Value);
                    break;
                case "tool":
                    chatHistory.Add(new ChatMessageContent(AuthorRole.Tool, item.Value));
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
            var stepData = await MakeApiCall(chatHistory, maxToken, false, temperature, clientKernel);
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

            chatHistory.AddAssistantMessage(JsonSerializer.Serialize(stepData, _jsonSerializerOptions));

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

        var history = new ChatHistory();

        foreach (MakeResultDto step in steps)
        {
            if (!string.IsNullOrWhiteSpace(step.Content))
            {
                history.AddAssistantMessage(step.Content);
            }
        }

        history.AddUserMessage(
            """
            Based on all our previous reasoning and analysis, please provide a comprehensive final solution that:
            
            1. Naturally flows from our previous thinking steps
            2. Shows how each reasoning path contributed to our understanding
            3. Explains how we arrived at this conclusion
            4. Addresses any concerns or limitations we discovered
            5. Provides practical, actionable guidance
            
            Remember to:
            - Build upon insights from each previous step
            - Keep explanations clear and natural
            - Focus on what matters most to solve the problem
            - Use appropriate level of detail based on the user's needs
            - Connect everything back to the original question
            
            Your response should feel like a natural conclusion to our thought process, not a structured template.
            
            """);

        foreach (var message in chatMessages)
        {
            history.Add(message);
        }

        await foreach (var content in MakeApiCallStreamAsync(history, maxToken, temperature, clientKernel))
        {
            sb.Append(content);
            yield return new MakeResultDto("Final Answer", content, MakeResultDto.FinalAnswerKey,
                MakeResultDto.MakeResultType.FinalAnswer);
        }

        totalThinkingTime += (endTime - startTime).TotalSeconds;
    }

    private async IAsyncEnumerable<string> MakeApiCallStreamAsync(ChatHistory history, int maxToken,
        float? temperature = null,
        Kernel? clientKernel = null)
    {
        IChatCompletionService chatCompletionService = clientKernel != null
            ? clientKernel.GetRequiredService<IChatCompletionService>()
            : kernel.GetRequiredService<IChatCompletionService>();


        await foreach (var streamingChatUpdate in chatCompletionService.GetStreamingChatMessageContentsAsync(history,
                           new OpenAIPromptExecutionSettings()
                           {
                               Temperature = temperature,
                               MaxTokens = maxToken,
                               TopP = options.Value.TopP,
                               // LogitBiases = { options.Value.LogitBias },
                               FrequencyPenalty = options.Value.FrequencyPenalty,
                               PresencePenalty = options.Value.PresencePenalty,
                           }))
        {
            yield return streamingChatUpdate.Content ?? string.Empty;
        }
    }

    public async Task<MakeResultDto> MakeApiCall(ChatHistory history, int maxToken,
        bool isFinalAnswer = false, float? temperature = null, Kernel? clientKernel = null)
    {
        IChatCompletionService chatCompletionService = clientKernel != null
            ? clientKernel.GetRequiredService<IChatCompletionService>()
            : kernel.GetRequiredService<IChatCompletionService>();

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response =
                    await chatCompletionService.GetChatMessageContentAsync(history, new OpenAIPromptExecutionSettings()
                    {
                        ResponseFormat = typeof(MakeResultDto),
                        // ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
                        Temperature = temperature,
                        MaxTokens = maxToken,
                        TopP = options.Value.TopP,
                        // LogitBiases = { options.Value.LogitBias },
                        FrequencyPenalty = options.Value.FrequencyPenalty,
                        PresencePenalty = options.Value.PresencePenalty,
                    });

                var content = response.Content;

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