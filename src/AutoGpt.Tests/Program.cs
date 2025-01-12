using System.Diagnostics;
using System.Text.Json;

using AutoGpt;

using Microsoft.SemanticKernel;

#pragma warning disable SKEXP0010

var service = new ServiceCollection()
        // .AddOpenAI((options =>
        // {
        //     options.Endpoint = "https://api.token-ai.cn/";
        //     options.NumOutputs = 8;
        // }));
        .AddAutoGpt((options =>
        {
            options.NumOutputs = 8;
        }))
    ;

service
    .AddKernel()
    .AddOpenAIChatCompletion("gpt-4o-mini", new Uri("https://api.token-ai.cn/v1"),
        "sk-ePuBDXM8k6nbBSdGeAewZA8HiJzgHRu7VMkqDeHoU4aYCRq6zg", "AutoGpt");

var serviceProvider = service.BuildServiceProvider();

var autoGptClient = serviceProvider.GetRequiredService<AutoGptClient>();

const string prompt =
    """
    用5升和6升的容器如何如何取3升的水？
    """;
var sw = Stopwatch.StartNew();
await foreach (var make in autoGptClient.GenerateResponseAsync(
                   prompt, 1000))
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