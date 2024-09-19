using System.Diagnostics;

using AutoGpt;

var service = new ServiceCollection()
    .AddAutoGpt((options =>
    {
        options.Endpoint = "https://api.token-ai.cn/";
        options.NumOutputs = 5;
    }));

var serviceProvider = service.BuildServiceProvider();

var autoGptClient = serviceProvider.GetRequiredService<AutoGptClient>();

var sw = Stopwatch.StartNew();
await foreach (var (title, content, totalThinkingTime) in autoGptClient.GenerateResponseAsync(
                   "c#使用Redis+RabbitMQ实现多级缓存", "sk-", "gpt-4o-mini-2024-07-18", 2000))
{
    if (title.StartsWith("Final Answer"))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(content);

        Console.ResetColor();
    }
    else
    {
        Console.WriteLine(title);
        Console.WriteLine(content);
    }
}

Console.WriteLine();

sw.Stop();

Console.WriteLine("生成总耗时：" + sw.ElapsedMilliseconds + "ms");