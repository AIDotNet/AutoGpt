﻿using System.Diagnostics;
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

var sw = Stopwatch.StartNew();
await foreach (var make in autoGptClient.GenerateResponseAsync(
                   "使用c#设计工厂模式，并且给出代码案例", "", "gpt-4o-2024-05-13",
                   2000))
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