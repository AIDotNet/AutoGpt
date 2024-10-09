using AutoGpt.Options;

using Microsoft.Extensions.DependencyInjection;

namespace AutoGpt;

public static class ServiceExtensions
{
    /// <summary>
    /// 添加 OpenAI 服务
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public static IServiceCollection AddOpenAI(this IServiceCollection services, Action<AutoGptOptions> configure)
    {
        services.AddAutoGpt(configure);

        services.AddSingleton<IClientFactory, OpenAiClientFactory>();

        return services;
    }

    public static IServiceCollection AddAzureOpenAI(this IServiceCollection services, Action<AutoGptOptions> configure)
    {
        services.AddAutoGpt(configure);

        services.AddSingleton<IClientFactory, AzureOpenAIClientFactory>();

        return services;
    }


    private static IServiceCollection AddAutoGpt(this IServiceCollection services, Action<AutoGptOptions> configure)
    {
        var options = new AutoGptOptions();

        services.Configure(configure);

        configure(options);

        options.Validate();

        services.AddSingleton<IPromptManager, PromptManager>();

        services.AddScoped<AutoGptClient>();

        services.AddHttpClient();

        services.AddSingleton<OpenAiClientFactory>();
        return services;
    }
}