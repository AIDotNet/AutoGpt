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

public class AutoGptClient(OpenAIAPI openAiApi, IOptions<AutoGptOptions> options)
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Dictionary<string, string> Prompt = new()
    {
        {
            "system",
            """
            You are an expert AI assistant that explains your reasoning step by step. For each step, provide a title that describes what you're doing in that step, along with the content. Decide if you need another step or if you're ready to give the final answer. Respond in JSON format with 'title', 'content', and 'next_action' (either 'continue' or 'final_answer') keys. USE AS MANY REASONING STEPS AS POSSIBLE. AT LEAST 3. BE AWARE OF YOUR LIMITATIONS AS AN LLM AND WHAT YOU CAN AND CANNOT DO. IN YOUR REASONING, INCLUDE EXPLORATION OF ALTERNATIVE ANSWERS. CONSIDER YOU MAY BE WRONG, AND IF YOU ARE WRONG IN YOUR REASONING, WHERE IT WOULD BE. FULLY TEST ALL OTHER POSSIBILITIES. YOU CAN BE WRONG. WHEN YOU SAY YOU ARE RE-EXAMINING, ACTUALLY RE-EXAMINE, AND USE ANOTHER APPROACH TO DO SO. DO NOT JUST SAY YOU ARE RE-EXAMINING. USE AT LEAST 3 METHODS TO DERIVE THE ANSWER. USE BEST PRACTICES.
            Do not output "{" and "}".
            Example of a valid JSON response:
            [{
                "title": "Identifying Key Information",
                "content": "To begin solving this problem, we need to carefully examine the given information and identify the crucial elements that will guide our solution process. This involves...",
                "next_action": "continue"
            }]
            """
        },
        {
            "assistant",
            "Thank you! I will now think step by step following my instructions, starting at the beginning after decomposing the problem."
        }
    };

    /// <summary>
    /// Generate response based on the chat history.
    /// </summary>
    /// <param name="prompt"></param>
    /// <param name="maxToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<(string title, string content, double thinkingTime)> GenerateResponseAsync(
        string prompt, int maxToken = 800)
    {
        var chatHistory = new List<ChatMessage>();
        foreach (var item in Prompt)
        {
            chatHistory.Add(new ChatMessage(ChatMessageRole.FromString(item.Key), item.Value));
        }

        chatHistory.Add(new ChatMessage(ChatMessageRole.User, prompt));

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
            var stepData = await MakeApiCall(chatHistory, maxToken);
            endTime = DateTime.Now;
            var thinkingTime = (endTime - startTime).TotalSeconds;
            totalThinkingTime += thinkingTime;

            steps.AddRange(stepData);

            if (stepData.Any(x => x.NextAction == "final_answer") ||
                stepCount > options.Value.NumOutputs) // Max number of steps
            {
                break;
            }

            chatHistory.Add(new ChatMessage(ChatMessageRole.Assistant,
                JsonSerializer.Serialize(stepData, _jsonSerializerOptions)));

            stepCount += 1;
        }

        startTime = DateTime.Now;

        endTime = DateTime.Now;
        var sb = new StringBuilder();

        var history = steps.Select(x => new ChatMessage(ChatMessageRole.Assistant, x.Content)).ToList();

        history.Add(new ChatMessage(ChatMessageRole.User,
            "Please help the user give the best solution based on your reasoning above, if possible in as much detail as possible."));

        history.Add(new ChatMessage(ChatMessageRole.User, prompt));

        await foreach (var content in MakeApiCallStreamAsync(history.ToArray(), maxToken))
        {
            sb.Append(content);
            yield return ($"Final Answer", content, totalThinkingTime);
        }

        totalThinkingTime += (endTime - startTime).TotalSeconds;
    }

    private async IAsyncEnumerable<string> MakeApiCallStreamAsync(ChatMessage[] history, int maxToken)
    {
        await foreach (var item in openAiApi.Chat.StreamChatEnumerableAsync(new ChatRequest()
                       {
                           Messages = history,
                           MaxTokens = maxToken,
                           Model = options.Value.Model,
                           Temperature = 0.2
                       }))
        {
            var content = item.Choices.FirstOrDefault()?.Message.TextContent ?? string.Empty;

            yield return content;
        }
    }

    public async Task<List<MakeResultDto>> MakeApiCall(List<ChatMessage> history, int maxToken,
        bool isFinalAnswer = false)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var response =
                    await openAiApi.Chat.CreateChatCompletionAsync(new ChatRequest()
                    {
                        Messages = history.ToArray(),
                        MaxTokens = maxToken,
                        Model = options.Value.Model,
                        //json数组
                        Temperature = 0.2
                    });


                var content = response.Choices.FirstOrDefault()?.Message.TextContent ?? string.Empty;

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
                            new MakeResultDto("Error",
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

    public MakeResultDto(string title, string content, string nextAction)
    {
        Title = title;
        Content = content;
        NextAction = nextAction;
    }

    [JsonPropertyName("title")] public string? Title { get; set; }

    [JsonPropertyName("content")] public string? Content { get; set; }

    [JsonPropertyName("next_action")] public string? NextAction { get; set; }
}