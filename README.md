<div align="center"><a name="readme-top"></a>
<img height="160" src="https://avatars.githubusercontent.com/u/163431636?s=96&v=4">

<h1>AutoGpt</h1>

AutoGpt 智能推理SDK，利用AI本身的能力进行多次对话推理，回复效果更加理想，更智能。

[![][npm-release-shield]][npm-release-link]
[![][github-releasedate-shield]][github-releasedate-link]<br/>
[![][github-contributors-shield]][github-contributors-link]
[![][github-forks-shield]][github-forks-link]
[![][github-stars-shield]][github-stars-link]
[![][github-issues-shield]][github-issues-link]
[![][github-license-shield]][github-license-link]

[Changelog](./CHANGELOG.md) · [Report Bug][github-issues-link] · [Request Feature][github-issues-link]

![](https://raw.githubusercontent.com/andreasbm/readme/master/assets/lines/rainbow.png)

</div>

[npm-release-shield]: https://img.shields.io/npm/v/@lobehub/chat?color=369eff&labelColor=black&logo=npm&logoColor=white&style=plastic
[npm-release-link]: https://www.npmjs.com/package/@lobehub/chat
[github-releasedate-shield]: https://img.shields.io/github/release-date/AIDotNet/AutoGpt?color=black&labelColor=black&style=plastic
[github-releasedate-link]: https://github.com/AIDotNet/AutoGpt/releases
[github-contributors-shield]: https://img.shields.io/github/contributors/AIDotNet/AutoGpt?color=c4f042&labelColor=black&style=plastic
[github-contributors-link]: https://github.com/AIDotNet/AutoGpt/graphs/contributors
[github-forks-shield]: https://img.shields.io/github/forks/AIDotNet/AutoGpt?color=8ae8ff&labelColor=black&style=plastic
[github-forks-link]: https://github.com/AIDotNet/AutoGpt/network/members
[github-stars-shield]: https://img.shields.io/github/stars/AIDotNet/AutoGpt?color=ffcb47&labelColor=black&style=plastic
[github-stars-link]: https://github.com/AIDotNet/AutoGpt/network/stargazers
[github-issues-shield]: https://img.shields.io/github/issues/AIDotNet/AutoGpt?color=ff80eb&labelColor=black&style=plastic
[github-issues-link]: https://github.com/AIDotNet/AutoGpt/issues
[github-license-shield]: https://img.shields.io/github/license/AIDotNet/AutoGpt?color=white&labelColor=black&style=plastic
[github-license-link]: https://github.com/AIDotNet/AutoGpt/blob/main/LICENSE



## 入门教程

创建项目`AutoGpt.Tests`控制台程序

然后安装NuGet包

```
dotnet add package AIDotNet.AutoGpt
```

安装完成以后打开我们的AI平台`http://api.token-ai.cn/`注册一个账号，然后登录到控制台，打开`AI服务/令牌`，然后点击复制Key

![图片](https://mmbiz.qpic.cn/mmbiz_png/CY59oiaHqeicSSun9579YVzqYAQbYoaEYYhDJYLM4vtAIy9BOZLodDguhwL7afs3kuw2Wx0wpBjFES1cyeffTKjg/640?wx_fmt=png&from=appmsg&wxfrom=13&tp=wxpic)

```
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
```

Options中的`Endpoint`是访问的AI API端点，然后`NumOutputs`则是推理最大次数。

然后拿到我们的`autoGptClient`调用`GenerateResponseAsync`方法，传递prompt`c#使用Redis+RabbitMQ实现多级缓存`，然后把`sk-`替换成我们刚刚在平台复制的Key，然后模型可以自己选择，默认使用gpt-4o-mini成本会更低，2000则是响应的最token数量。

然后如果title 前缀是`Final Answer`那么数据分析完成，得到content则是实际对话响应内容。

### **尝试推理对话效果。**

````markdown
实现一个基于Redis和RabbitMQ的多级缓存系统，可以有效提高数据访问速度和系统的可扩展性。以下是详细的步骤和示例代码，帮助你在C#中实现这个系统。

### 1. 环境准备

确保你已经安装了以下组件：
- **Redis**：可以在本地或使用云服务。
- **RabbitMQ**：同样可以在本地或使用云服务。
- **C#开发环境**：如Visual Studio。

### 2. 安装必要的NuGet包

在你的C#项目中，安装以下NuGet包：
```bash
Install-Package StackExchange.Redis
Install-Package RabbitMQ.Client
```

### 3. Redis缓存实现

首先，我们需要创建一个Redis缓存的管理类。

```csharp
using StackExchange.Redis;
using System;

public class RedisCache
{
    private readonly IDatabase _database;

    public RedisCache(string connectionString)
    {
        var redis = ConnectionMultiplexer.Connect(connectionString);
        _database = redis.GetDatabase();
    }

    public void Set(string key, string value, TimeSpan? expiry = null)
    {
        _database.StringSet(key, value, expiry);
    }

    public string Get(string key)
    {
        return _database.StringGet(key);
    }

    public void Delete(string key)
    {
        _database.KeyDelete(key);
    }
}
```

### 4. RabbitMQ消息处理

接下来，我们需要创建一个RabbitMQ的管理类，用于发送和接收消息。

```csharp
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

public class RabbitMqService
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqService(string hostName)
    {
        var factory = new ConnectionFactory() { HostName = hostName };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(queue: "cache_invalidation",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);
    }

    public void SendMessage(string message)
    {
        var body = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(exchange: "",
                             routingKey: "cache_invalidation",
                             basicProperties: null,
                             body: body);
    }

    public void StartListening(Action<string> onMessageReceived)
    {
        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            onMessageReceived(message);
        };
        _channel.BasicConsume(queue: "cache_invalidation",
                             autoAck: true,
                             consumer: consumer);
    }
}
```

### 5. 整合Redis和RabbitMQ

在你的应用程序中，整合Redis和RabbitMQ，处理数据的缓存和失效。

```csharp
public class CacheService
{
    private readonly RedisCache _redisCache;
    private readonly RabbitMqService _rabbitMqService;

    public CacheService(string redisConnectionString, string rabbitMqHostName)
    {
        _redisCache = new RedisCache(redisConnectionString);
        _rabbitMqService = new RabbitMqService(rabbitMqHostName);

        // 启动监听
        _rabbitMqService.StartListening(OnCacheInvalidation);
    }

    public string GetData(string key)
    {
        // 先从Redis获取数据
        var cachedData = _redisCache.Get(key);
        if (cachedData != null)
        {
        _rabbitMqService.SendMessage(key);
    }

    private void OnCacheInvalidation(string key)
    {
        // 接收到失效消息后，删除Redis中的缓存
        _redisCache.Delete(key);
    }

    private string GetDataFromDatabase(string key)
    {
        // 模拟从数据库获取数据
        return $"Data for {key}";
    }

    private void UpdateDatabase(string key, string newValue)
    {
        // 模拟更新数据库
        Console.WriteLine($"Database updated: {key} = {newValue}");
    }
}
```

### 6. 使用示例

最后，在你的主程序中使用这个缓存服务。

```csharp
class Program
{
    static void Main(string[] args)
    {
        var cacheService = new CacheService("localhost:6379", "localhost");

        // 获取数据
        var data = cacheService.GetData("key1");
        Console.WriteLine(data);

        // 更新数据
        cacheService.UpdateData("key1", "New Value");

        // 再次获取数据
        var updatedData = cacheService.GetData("key1");
        Console.WriteLine(updatedData);
    }
}
```

### 7. 总结

以上代码展示了如何在C#中使用Redis和RabbitMQ实现一个简单的多级缓存系统。通过Redis来快速存取数据，并使用RabbitMQ来处理缓存失效的消息，从而保持数据的一致性。你可以根据具体需求扩展和优化这个系统，例如添加错误处理、日志记录等功能。
````

### **没使用推理对话效果**

````markdown
在C#中使用Redis和RabbitMQ实现多级缓存的架构可以提高应用的性能和响应速度。下面是一个简单的实现思路和代码示例：

### 1. 系统架构

- **Redis**: 用作快速缓存存储，适合存储频繁访问的数据。
- **RabbitMQ**: 用作消息队列，处理数据更新或缓存失效的通知。

### 2. 安装依赖

确保你在项目中安装了以下NuGet包：

```bash
Install-Package StackExchange.Redis
Install-Package RabbitMQ.Client
```

### 3. 实现逻辑

以下是一个简单的实现示例：

```csharp
using StackExchange.Redis;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;

class Program
{
    private static ConnectionMultiplexer redis;
    private static IConnection rabbitConnection;
    private static IModel rabbitChannel;

    static void Main(string[] args)
    {
        // 初始化Redis
        redis = ConnectionMultiplexer.Connect("localhost");
        
        // 初始化RabbitMQ
        var factory = new ConnectionFactory() { HostName = "localhost" };
        rabbitConnection = factory.CreateConnection();
        rabbitChannel = rabbitConnection.CreateModel();
        rabbitChannel.QueueDeclare(queue: "cache_updates", durable: false, exclusive: false, autoDelete: false, arguments: null);
        
        // 启动RabbitMQ消费者
        var consumer = new EventingBasicConsumer(rabbitChannel);
        consumer.Received += Consumer_Received;
        rabbitChannel.BasicConsume(queue: "cache_updates", autoAck: true, consumer: consumer);

        // 示例数据操作
        SetData("key1", "value1");

        // 等待用户输入
        Console.ReadLine();
    }

    static void SetData(string key, string value)
    {
        var db = redis.GetDatabase();
        db.StringSet(key, value);
        
        // 发送消息到RabbitMQ
        var body = Encoding.UTF8.GetBytes(key);
        rabbitChannel.BasicPublish(exchange: "", routingKey: "cache_updates", basicProperties: null, body: body);
    }

    static void Consumer_Received(object sender, BasicDeliverEventArgs e)
    {
        var key = Encoding.UTF8.GetString(e.Body.ToArray());
        var db = redis.GetDatabase();
        
        // 从Redis删除缓存
        db.KeyDelete(key);
        Console.WriteLine($"Cache for {key} has been invalidated.");
    }
}
```

### 4. 代码说明

- **Redis连接**: 使用`StackExchange.Redis`库连接Redis。
- **RabbitMQ连接**: 使用`RabbitMQ.Client`库连接RabbitMQ，并创建一个消息队列`cache_updates`。
- **数据设置**: `SetData`方法将数据存入Redis，并发送消息到RabbitMQ。
- **消费者**: 在`Consumer_Received`中，接收来自RabbitMQ的消息并从Redis中删除相应的缓存。

### 5. 运行示例

1. 确保Redis和RabbitMQ服务正在运行。
2. 编译并运行上述代码。
3. 通过调用`SetData`方法设置数据并触发缓存更新。

### 6. 扩展

- **缓存读取**: 你可以扩展代码，加入从Redis读取数据的逻辑。
- **错误处理**: 增加异常处理和日志记录。
- **配置管理**: 将Redis和RabbitMQ的连接字符串放在配置文件中。

这个示例提供了一个基础的多级缓存实现，具体的应用场景和需求可以根据项目需要进行调整和优化。
````

### **AI评分**

```markdown
### 智能推理内容评分：8/10

**优点：**
1. **详细性**：提供了完整的代码示例和逐步的实现步骤，便于开发者理解和实践。
2. **结构清晰**：内容分为多个部分，易于导航，逻辑清晰。
3. **集成示例**：展示了如何将Redis和RabbitMQ结合使用，适合需要实现多级缓存的开发者。

**缺点：**
1. **复杂性**：对于初学者来说，Redis和RabbitMQ的概念可能会比较复杂，缺乏简单的解释。
2. **缺少错误处理示例**：虽然提到可以扩展，但没有具体的错误处理示例。

### 普通内容评分：7/10

**优点：**
1. **简洁性**：代码相对简单，适合初学者理解基本概念。
2. **基础实现**：提供了一个简单的实现思路，适合快速入门。

**缺点：**
1. **缺乏深度**：没有详细的步骤说明，可能对初学者不够友好。
2. **功能有限**：示例代码功能较少，没有展示如何处理缓存失效的完整流程。

### 总结

**哪个效果更好：** 智能内容效果更好。虽然普通内容更简洁，但智能内容提供了更全面的实现细节和背景信息，适合需要深入理解和实施的开发者。智能内容的结构和示例更有助于开发者在实际项目中应用。
```

## **结束**

https://open666.cn/ 已经接入了自动推理功能

![图片](https://mmbiz.qpic.cn/mmbiz_png/CY59oiaHqeicSSun9579YVzqYAQbYoaEYY4NXk4QL6GVnoq9YRXhhMTwf613xH2WYoib5tNnT9k1XPuiamu9U3ahRw/640?wx_fmt=png&from=appmsg&tp=wxpic&wxfrom=5&wx_lazy=1&wx_co=1)

如果您想分享DotNet技术qq群：

<img height="400" src="https://mmbiz.qpic.cn/mmbiz_png/CY59oiaHqeicSSun9579YVzqYAQbYoaEYYdDrKCy5lDpdicyeU4AOILH8ibYTk4SPkIhlibH3RxCM0MsTIBzx6I5cEw/640?wx_fmt=png&from=appmsg&tp=wxpic&wxfrom=5&wx_lazy=1&wx_co=1">

AIDotNet微信社区群：

<img height="400" src="https://mmbiz.qpic.cn/mmbiz_png/CY59oiaHqeicSSun9579YVzqYAQbYoaEYY6zV3zpYpmw8MVXy5VoZydA6f0iaOFFfwaqNTsOTstfYFoktgpfg9g2w/640?wx_fmt=png&from=appmsg&tp=wxpic&wxfrom=5&wx_lazy=1&wx_co=1">
