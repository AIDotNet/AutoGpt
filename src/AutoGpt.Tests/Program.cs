using System.Diagnostics;
using System.Text.Json;

using AutoGpt;

var service = new ServiceCollection()
    // .AddOpenAI((options =>
    // {
    //     options.Endpoint = "https://api.token-ai.cn/";
    //     options.NumOutputs = 8;
    // }));
    .AddAzureOpenAI((options =>
    {
        options.Endpoint = "https://token-ai-east.openai.azure.com/";
        options.NumOutputs = 8;
    }));

var serviceProvider = service.BuildServiceProvider();

var autoGptClient = serviceProvider.GetRequiredService<AutoGptClient>();

const string prompt =
    """
    如果一只蜗牛白天爬上 10 英尺高的杆子，然后晚上从 6 英尺高的杆子上滑下来，那么蜗牛需要多少天才能到达顶端？
    """;
// 周长为 18 英尺。 （矩形周长的公式为 P = 2 *（长度 + 宽度）。在本例中，P = 2 * (4 + 5) = 2 * 9 = 18 英尺。）
var sw = Stopwatch.StartNew();
await foreach (var make in autoGptClient.GenerateResponseAsync(
                   prompt, "", "gpt-4o",
                   500, promptHandler: (pairs) =>
                   {
                       foreach (KeyValuePair<string, string> pair in pairs)
                       {
                           if (pair.Key == "system")
                           {
                               pairs[pair.Key] =
                                   """
                                   You are an expert AI assistant that explains your reasoning step by step. For each step, provide a title that describes what you're doing in that step, along with the content. Decide if you need another step or if you're ready to give the final answer. Respond in JSON format with 'title', 'content', and 'next_action' (either 'continue' or 'final_answer') keys. USE AS MANY REASONING STEPS AS POSSIBLE. AT LEAST 3. BE AWARE OF YOUR LIMITATIONS AS AN LLM AND WHAT YOU CAN AND CANNOT DO. IN YOUR REASONING, INCLUDE EXPLORATION OF ALTERNATIVE ANSWERS. CONSIDER YOU MAY BE WRONG, AND IF YOU ARE WRONG IN YOUR REASONING, WHERE IT WOULD BE. FULLY TEST ALL OTHER POSSIBILITIES. YOU CAN BE WRONG. WHEN YOU SAY YOU ARE RE-EXAMINING, ACTUALLY RE-EXAMINE, AND USE ANOTHER APPROACH TO DO SO. DO NOT JUST SAY YOU ARE RE-EXAMINING. USE AT LEAST 3 METHODS TO DERIVE THE ANSWER. USE BEST PRACTICES.
                                   Example of a valid JSON response:
                                   ```json
                                   {
                                       "title": "Identifying Key Information",
                                       "content": "To begin solving this problem, we need to carefully examine the given information and identify the crucial elements that will guide our solution process. This involves...",
                                       "next_action": "continue"
                                   }```                                       
                                   """;
                           }
                       }
                   }))
{
    if (make.Type == MakeResultDto.MakeResultType.FinalAnswer)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(make.Content);

        Console.ResetColor();
    }
    else
    {
        Console.WriteLine("推理标题：" + make.Title);
        Console.WriteLine("推理内容：" + make.Content);
    }
}

Console.WriteLine();

sw.Stop();

Console.WriteLine("生成总耗时：" + sw.ElapsedMilliseconds + "ms");